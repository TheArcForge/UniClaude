import { test } from "node:test";
import { strict as assert } from "node:assert";
import { toStandardPhase1 } from "../src/commands/to-standard.mjs";
import {
  mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

function setupNinjaProject() {
  const root = mkdtempSync(join(tmpdir(), "uc-ninja-"));
  spawnSync("git", ["init", "-q", "-b", "main"], { cwd: root });
  spawnSync("git", ["config", "user.email", "t@e.st"], { cwd: root });
  spawnSync("git", ["config", "user.name", "test"], { cwd: root });

  mkdirSync(join(root, "Packages", "com.arcforge.uniclaude"), { recursive: true });
  writeFileSync(join(root, "Packages", "manifest.json"),
    JSON.stringify({ dependencies: { "com.unity.ugui": "2.0.0" } }, null, 2) + "\n");
  writeFileSync(join(root, "Packages", "packages-lock.json"),
    JSON.stringify({
      dependencies: {
        "com.arcforge.uniclaude": { version: "git+...", dependencies: {} },
        "com.unity.ugui": { version: "2.0.0", source: "builtin", dependencies: {} },
      },
    }, null, 2) + "\n");
  writeFileSync(join(root, "Packages", "com.arcforge.uniclaude", "package.json"),
    JSON.stringify({
      name: "com.arcforge.uniclaude",
      version: "0.1.0",
      repository: { url: "https://github.com/TheArcForge/UniClaude.git" },
    }, null, 2) + "\n");

  mkdirSync(join(root, ".git", "info"), { recursive: true });
  writeFileSync(join(root, ".git", "info", "exclude"),
    "# UniClaude ninja-mode (managed by UniClaude — do not edit)\nPackages/com.arcforge.uniclaude/\n");
  writeFileSync(join(root, ".git", "info", "attributes"),
    "Packages/packages-lock.json filter=uniclaude\n");
  spawnSync("git", ["config", "filter.uniclaude.clean", "echo"], { cwd: root });
  spawnSync("git", ["config", "filter.uniclaude.smudge", "echo"], { cwd: root });
  spawnSync("git", ["config", "filter.uniclaude.required", "true"], { cwd: root });

  mkdirSync(join(root, "Library", "UniClaude"), { recursive: true });
  writeFileSync(join(root, "Library", "UniClaude", "filter-manifest.json"),
    JSON.stringify({ owned: { "com.arcforge.uniclaude": { version: "...", dependencies: {} } } }, null, 2) + "\n");
  writeFileSync(join(root, "Library", "UniClaude", "installer-persistent.mjs"), "// stub\n");

  return { dir: root, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}

test("to-standard: uninstalls filter, strips sentinel, restores manifest, writes marker + staged status", () => {
  const r = setupNinjaProject();
  try {
    const result = toStandardPhase1({
      projectRoot: r.dir,
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Packages", "com.arcforge.uniclaude", "Installer~", "installer.mjs"),
      unityPid: 4242,
      unityAppPath: "/Applications/Unity.app/Contents/MacOS/Unity",
    });

    assert.equal(result.result, "ok");
    assert.equal(result.mode, "standard-pending");
    assert.equal(result.markerPath, join(r.dir, "Library", "UniClaude", "pending-transition.json"));

    const manifest = JSON.parse(readFileSync(join(r.dir, "Packages", "manifest.json"), "utf8"));
    assert.equal(
      manifest.dependencies["com.arcforge.uniclaude"],
      "https://github.com/TheArcForge/UniClaude.git");

    const excl = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    assert.doesNotMatch(excl, /UniClaude ninja-mode/);

    const attrs = readFileSync(join(r.dir, ".git", "info", "attributes"), "utf8");
    assert.doesNotMatch(attrs, /filter=uniclaude/);

    const clean = spawnSync("git", ["config", "--get", "filter.uniclaude.clean"], { cwd: r.dir });
    assert.notEqual(clean.status, 0);

    const marker = JSON.parse(readFileSync(result.markerPath, "utf8"));
    assert.equal(marker.kind, "to-standard");
    assert.equal(marker.unityPid, 4242);
    assert.equal(marker.unityAppPath, "/Applications/Unity.app/Contents/MacOS/Unity");
    assert.equal(marker.projectPath, r.dir);
    assert.equal(marker.packagePath, join(r.dir, "Packages", "com.arcforge.uniclaude"));
    assert.equal(marker.statusPath, join(r.dir, "Library", "UniClaude", "transition-status.json"));
    assert.match(marker.createdAt, /^\d{4}-\d{2}-\d{2}T/);

    const status = JSON.parse(readFileSync(marker.statusPath, "utf8"));
    assert.equal(status.command, "to-standard");
    assert.equal(status.step, "staged");
    assert.equal(status.result, "in-progress");
  } finally { r.cleanup(); }
});

test("to-standard: prefers filter-manifest.originalSpec over package.json repository.url", () => {
  const r = setupNinjaProject();
  try {
    writeFileSync(join(r.dir, "Library", "UniClaude", "filter-manifest.json"),
      JSON.stringify({
        owned: { "com.arcforge.uniclaude": { version: "...", dependencies: {} } },
        originalSpec: "git+https://github.com/TheArcForge/UniClaude.git#feature/ninja-mode",
      }, null, 2) + "\n");

    toStandardPhase1({
      projectRoot: r.dir,
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Packages", "com.arcforge.uniclaude", "Installer~", "installer.mjs"),
      unityPid: 1,
      unityAppPath: "/u",
    });

    const manifest = JSON.parse(readFileSync(join(r.dir, "Packages", "manifest.json"), "utf8"));
    assert.equal(
      manifest.dependencies["com.arcforge.uniclaude"],
      "git+https://github.com/TheArcForge/UniClaude.git#feature/ninja-mode");
  } finally { r.cleanup(); }
});

test("to-standard: copies src/ alongside persistent installer", () => {
  const r = setupNinjaProject();
  try {
    const installerDir = join(r.dir, "InstallerSource");
    mkdirSync(join(installerDir, "src", "commands"), { recursive: true });
    writeFileSync(join(installerDir, "installer.mjs"), "// entry\n");
    writeFileSync(join(installerDir, "src", "a.mjs"), "// a\n");
    writeFileSync(join(installerDir, "src", "commands", "b.mjs"), "// b\n");

    toStandardPhase1({
      projectRoot: r.dir,
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(installerDir, "installer.mjs"),
      unityPid: 1,
      unityAppPath: "/u",
    });

    assert.ok(existsSync(join(r.dir, "Library", "UniClaude", "installer-persistent.mjs")));
    assert.ok(existsSync(join(r.dir, "Library", "UniClaude", "src", "a.mjs")));
    assert.ok(existsSync(join(r.dir, "Library", "UniClaude", "src", "commands", "b.mjs")));
  } finally { r.cleanup(); }
});
