import { test } from "node:test";
import { strict as assert } from "node:assert";
import { toNinja } from "../src/commands/to-ninja.mjs";
import {
  mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

function setupProject() {
  const root = mkdtempSync(join(tmpdir(), "uc-proj-"));
  spawnSync("git", ["init", "-q", "-b", "main"], { cwd: root });
  spawnSync("git", ["config", "user.email", "t@e.st"], { cwd: root });
  spawnSync("git", ["config", "user.name", "test"], { cwd: root });

  mkdirSync(join(root, "Packages"), { recursive: true });
  writeFileSync(join(root, "Packages", "manifest.json"),
    JSON.stringify({
      dependencies: {
        "com.arcforge.uniclaude": "https://github.com/TheArcForge/UniClaude.git",
        "com.unity.ugui": "2.0.0",
      },
    }, null, 2) + "\n");
  writeFileSync(join(root, "Packages", "packages-lock.json"),
    JSON.stringify({
      dependencies: {
        "com.arcforge.uniclaude": {
          version: "https://github.com/TheArcForge/UniClaude.git",
          depth: 0,
          source: "git",
          dependencies: { "com.unity.nuget.newtonsoft-json": "3.2.1" },
          hash: "abc",
        },
        "com.unity.nuget.newtonsoft-json": {
          version: "3.2.1",
          depth: 1,
          source: "registry",
          dependencies: {},
          url: "https://packages.unity.com",
        },
        "com.unity.ugui": {
          version: "2.0.0",
          depth: 0,
          source: "builtin",
          dependencies: {},
        },
      },
    }, null, 2) + "\n");

  spawnSync("git", ["add", "."], { cwd: root });
  spawnSync("git", ["commit", "-q", "-m", "initial"], { cwd: root });
  return {
    dir: root,
    cleanup: () => rmSync(root, { recursive: true, force: true }),
  };
}

test("to-ninja: full happy path", () => {
  const r = setupProject();
  try {
    const result = toNinja({
      projectRoot: r.dir,
      gitUrl: "https://example.invalid/uniclaude.git",
      cloneFn: (url, dest) => {
        mkdirSync(dest, { recursive: true });
        writeFileSync(join(dest, "package.json"),
          JSON.stringify({
            name: "com.arcforge.uniclaude",
            version: "0.1.0",
            dependencies: { "com.unity.nuget.newtonsoft-json": "3.2.1" },
            repository: { url: "https://example.invalid/uniclaude.git" },
          }, null, 2) + "\n");
      },
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Installer~", "installer.mjs"),
    });

    assert.equal(result.result, "ok");

    const manifest = JSON.parse(readFileSync(join(r.dir, "Packages", "manifest.json"), "utf8"));
    assert.equal(manifest.dependencies["com.arcforge.uniclaude"], undefined);
    assert.equal(manifest.dependencies["com.unity.ugui"], "2.0.0");

    assert.ok(existsSync(join(r.dir, "Packages", "com.arcforge.uniclaude", "package.json")));

    const excl = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    assert.match(excl, /UniClaude ninja-mode/);

    const attrs = readFileSync(join(r.dir, ".git", "info", "attributes"), "utf8");
    assert.match(attrs, /filter=uniclaude/);

    const { stdout: clean } = spawnSync("git",
      ["config", "--get", "filter.uniclaude.clean"], { cwd: r.dir, encoding: "utf8" });
    assert.match(clean, /installer-persistent\.mjs/);
    assert.match(clean, /clean/);

    const { stdout: smudge } = spawnSync("git",
      ["config", "--get", "filter.uniclaude.smudge"], { cwd: r.dir, encoding: "utf8" });
    assert.match(smudge, /smudge/);

    const { stdout: required } = spawnSync("git",
      ["config", "--get", "filter.uniclaude.required"], { cwd: r.dir, encoding: "utf8" });
    assert.equal(required.trim(), "true");

    const fm = JSON.parse(readFileSync(
      join(r.dir, "Library", "UniClaude", "filter-manifest.json"), "utf8"));
    assert.deepEqual(Object.keys(fm.owned).sort(), [
      "com.arcforge.uniclaude",
      "com.unity.nuget.newtonsoft-json",
    ]);
    assert.equal(fm.originalSpec, "https://github.com/TheArcForge/UniClaude.git");
  } finally { r.cleanup(); }
});

test("to-ninja: preserves UPM-style originalSpec with branch pin", () => {
  const r = setupProject();
  try {
    // Overwrite the manifest with a realistic UPM spec that includes git+ prefix and #ref
    const upmSpec = "git+https://github.com/TheArcForge/UniClaude.git#feature/ninja-mode";
    const manifest = JSON.parse(readFileSync(join(r.dir, "Packages", "manifest.json"), "utf8"));
    manifest.dependencies["com.arcforge.uniclaude"] = upmSpec;
    writeFileSync(join(r.dir, "Packages", "manifest.json"),
      JSON.stringify(manifest, null, 2) + "\n");

    toNinja({
      projectRoot: r.dir,
      gitUrl: upmSpec,
      cloneFn: (url, dest) => {
        mkdirSync(dest, { recursive: true });
        writeFileSync(join(dest, "package.json"),
          JSON.stringify({ name: "com.arcforge.uniclaude", version: "0.1.0" }, null, 2) + "\n");
      },
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Installer~", "installer.mjs"),
    });

    const fm = JSON.parse(readFileSync(
      join(r.dir, "Library", "UniClaude", "filter-manifest.json"), "utf8"));
    assert.equal(fm.originalSpec, upmSpec);
  } finally { r.cleanup(); }
});

test("to-ninja: git filter config bakes absolute node binary path", () => {
  const r = setupProject();
  try {
    toNinja({
      projectRoot: r.dir,
      gitUrl: "https://example.invalid/uniclaude.git",
      cloneFn: (url, dest) => {
        mkdirSync(dest, { recursive: true });
        writeFileSync(join(dest, "package.json"),
          JSON.stringify({ name: "com.arcforge.uniclaude", version: "0.1.0" }, null, 2) + "\n");
      },
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Installer~", "installer.mjs"),
      nodeBinary: "/fake/abs/path/node",
    });

    const { stdout: clean } = spawnSync("git",
      ["config", "--get", "filter.uniclaude.clean"], { cwd: r.dir, encoding: "utf8" });
    assert.match(clean, /"\/fake\/abs\/path\/node"/);
    assert.match(clean, /installer-persistent\.mjs/);

    const { stdout: smudge } = spawnSync("git",
      ["config", "--get", "filter.uniclaude.smudge"], { cwd: r.dir, encoding: "utf8" });
    assert.match(smudge, /"\/fake\/abs\/path\/node"/);
  } finally { r.cleanup(); }
});

test("to-ninja: defaultClone handles git+file:// URL with branch fragment (real git)", () => {
  // Create a real upstream repo, seed it with a feature branch, then drive toNinja
  // through defaultClone (no cloneFn injection) with the UPM URL format that caused
  // the production failure: "git+file://...#branch".
  const upstream = mkdtempSync(join(tmpdir(), "uc-up-"));
  try {
    spawnSync("git", ["init", "-q", "-b", "main"], { cwd: upstream });
    spawnSync("git", ["config", "user.email", "t@e.st"], { cwd: upstream });
    spawnSync("git", ["config", "user.name", "test"], { cwd: upstream });
    writeFileSync(join(upstream, "package.json"),
      JSON.stringify({ name: "com.arcforge.uniclaude", version: "0.1.0" }, null, 2) + "\n");
    spawnSync("git", ["add", "."], { cwd: upstream });
    spawnSync("git", ["commit", "-q", "-m", "initial"], { cwd: upstream });
    spawnSync("git", ["checkout", "-q", "-b", "feature/ninja-mode"], { cwd: upstream });
    writeFileSync(join(upstream, "package.json"),
      JSON.stringify({ name: "com.arcforge.uniclaude", version: "0.2.0-ninja" }, null, 2) + "\n");
    spawnSync("git", ["commit", "-qam", "bump"], { cwd: upstream });

    const r = setupProject();
    try {
      const upmUrl = `git+file://${upstream}#feature/ninja-mode`;
      const result = toNinja({
        projectRoot: r.dir,
        gitUrl: upmUrl,
        // no cloneFn → exercises defaultClone with real git
        libraryRoot: join(r.dir, "Library", "UniClaude"),
        installerSourcePath: join(r.dir, "Installer~", "installer.mjs"),
      });
      assert.equal(result.result, "ok");

      // Verify the branch was checked out (feature branch's package.json has 0.2.0-ninja)
      const cloned = JSON.parse(readFileSync(
        join(r.dir, "Packages", "com.arcforge.uniclaude", "package.json"), "utf8"));
      assert.equal(cloned.version, "0.2.0-ninja");
    } finally { r.cleanup(); }
  } finally { rmSync(upstream, { recursive: true, force: true }); }
});

test("to-ninja: copies src/ tree so persistent installer can resolve imports", () => {
  const r = setupProject();
  try {
    // Seed a realistic Installer~ tree with src/ subdir
    const installerDir = join(r.dir, "Installer~");
    mkdirSync(join(installerDir, "src", "commands"), { recursive: true });
    writeFileSync(join(installerDir, "installer.mjs"), "// entry\n");
    writeFileSync(join(installerDir, "src", "filter-manifest.mjs"), "// dep\n");
    writeFileSync(join(installerDir, "src", "commands", "to-ninja.mjs"), "// nested dep\n");

    toNinja({
      projectRoot: r.dir,
      gitUrl: "https://example.invalid/uniclaude.git",
      cloneFn: (url, dest) => {
        mkdirSync(dest, { recursive: true });
        writeFileSync(join(dest, "package.json"),
          JSON.stringify({ name: "com.arcforge.uniclaude", version: "0.1.0" }, null, 2) + "\n");
      },
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(installerDir, "installer.mjs"),
    });

    assert.ok(existsSync(join(r.dir, "Library", "UniClaude", "installer-persistent.mjs")));
    assert.ok(existsSync(join(r.dir, "Library", "UniClaude", "src", "filter-manifest.mjs")));
    assert.ok(existsSync(join(r.dir, "Library", "UniClaude", "src", "commands", "to-ninja.mjs")));
  } finally { r.cleanup(); }
});

test("to-ninja: aborts when packages-lock.json is missing", () => {
  const r = setupProject();
  try {
    rmSync(join(r.dir, "Packages", "packages-lock.json"));
    assert.throws(() => toNinja({
      projectRoot: r.dir,
      gitUrl: "https://example.invalid/uniclaude.git",
      cloneFn: () => {},
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: join(r.dir, "Installer~", "installer.mjs"),
    }), /packages-lock\.json/);
  } finally { r.cleanup(); }
});
