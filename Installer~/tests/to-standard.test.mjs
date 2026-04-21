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

  // Pre-seed persistent installer so the impl doesn't throw when installerSourcePath is absent
  writeFileSync(join(root, "Library", "UniClaude", "installer-persistent.mjs"), "// stub\n");

  return { dir: root, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}

test("to-standard-phase1: uninstalls filter, strips sentinel, adds manifest entry, spawns deleter", () => {
  const r = setupNinjaProject();
  try {
    const spawned = [];
    const result = toStandardPhase1({
      projectRoot: r.dir,
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Packages", "com.arcforge.uniclaude", "Installer~", "installer.mjs"),
      spawnDetached: (cmd, args) => spawned.push({ cmd, args }),
    });

    assert.equal(result.result, "ok");

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

    assert.equal(spawned.length, 1);
    assert.match(spawned[0].args.join(" "), /delete-folder/);
    assert.match(spawned[0].args.join(" "), /Packages\/com\.arcforge\.uniclaude/);
  } finally { r.cleanup(); }
});
