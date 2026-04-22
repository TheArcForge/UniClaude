import { test } from "node:test";
import { strict as assert } from "node:assert";
import { deleteFromNinja } from "../src/commands/delete-from-ninja.mjs";
import {
  mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

function setupNinja() {
  const root = mkdtempSync(join(tmpdir(), "uc-delninja-"));
  spawnSync("git", ["init", "-q", "-b", "main"], { cwd: root });
  spawnSync("git", ["config", "user.email", "t@e.st"], { cwd: root });
  spawnSync("git", ["config", "user.name", "test"], { cwd: root });

  mkdirSync(join(root, "Packages", "com.arcforge.uniclaude"), { recursive: true });
  writeFileSync(join(root, "Packages", "manifest.json"),
    JSON.stringify({ dependencies: { "com.unity.ugui": "2.0.0" } }, null, 2) + "\n");

  mkdirSync(join(root, ".git", "info"), { recursive: true });
  writeFileSync(join(root, ".git", "info", "exclude"),
    "# UniClaude ninja-mode (managed by UniClaude — do not edit)\nPackages/com.arcforge.uniclaude/\n");
  writeFileSync(join(root, ".git", "info", "attributes"),
    "Packages/packages-lock.json filter=uniclaude\n");
  spawnSync("git", ["config", "filter.uniclaude.clean", "echo"], { cwd: root });
  spawnSync("git", ["config", "filter.uniclaude.smudge", "echo"], { cwd: root });
  spawnSync("git", ["config", "filter.uniclaude.required", "true"], { cwd: root });

  mkdirSync(join(root, "Library", "UniClaude"), { recursive: true });
  writeFileSync(join(root, "Library", "UniClaude", "installer-persistent.mjs"), "// stub\n");

  return { dir: root, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}

test("delete-from-ninja: uninstalls filter, strips sentinel, writes marker + staged status, leaves manifest alone", () => {
  const r = setupNinja();
  try {
    const result = deleteFromNinja({
      projectRoot: r.dir,
      libraryRoot: join(r.dir, "Library", "UniClaude"),
      installerSourcePath: "nonexistent",
      unityPid: 9001,
      unityAppPath: "/Applications/Unity.app/Contents/MacOS/Unity",
    });
    assert.equal(result.result, "ok");
    assert.equal(result.mode, "delete-pending");
    assert.equal(result.markerPath, join(r.dir, "Library", "UniClaude", "pending-transition.json"));

    const excl = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    assert.doesNotMatch(excl, /UniClaude ninja-mode/);

    const attrs = readFileSync(join(r.dir, ".git", "info", "attributes"), "utf8");
    assert.doesNotMatch(attrs, /filter=uniclaude/);

    const manifest = JSON.parse(readFileSync(join(r.dir, "Packages", "manifest.json"), "utf8"));
    assert.equal(manifest.dependencies["com.arcforge.uniclaude"], undefined, "no re-add");

    const marker = JSON.parse(readFileSync(result.markerPath, "utf8"));
    assert.equal(marker.kind, "delete-from-ninja");
    assert.equal(marker.unityPid, 9001);
    assert.equal(marker.packagePath, join(r.dir, "Packages", "com.arcforge.uniclaude"));

    const status = JSON.parse(readFileSync(marker.statusPath, "utf8"));
    assert.equal(status.command, "delete-from-ninja");
    assert.equal(status.step, "staged");
    assert.equal(status.result, "in-progress");
  } finally { r.cleanup(); }
});
