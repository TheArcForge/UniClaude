import { test } from "node:test";
import { strict as assert } from "node:assert";
import { git, gitOk } from "../src/git.mjs";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

function initRepo() {
  const d = mkdtempSync(join(tmpdir(), "uc-git-"));
  spawnSync("git", ["init", "-q", "-b", "main"], { cwd: d });
  spawnSync("git", ["config", "user.email", "t@e.st"], { cwd: d });
  spawnSync("git", ["config", "user.name", "test"], { cwd: d });
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("git returns stdout on success", () => {
  const r = initRepo();
  try {
    const result = git(r.dir, ["rev-parse", "--is-inside-work-tree"]);
    assert.equal(result.stdout.trim(), "true");
    assert.equal(result.exitCode, 0);
  } finally { r.cleanup(); }
});

test("git returns exitCode nonzero and captures stderr on failure", () => {
  const r = initRepo();
  try {
    const result = git(r.dir, ["diff", "--quiet", "nonexistent-file.txt"]);
    // `git diff --quiet` on a nonexistent path exits 0; use a reliably-failing invocation:
    const bad = git(r.dir, ["bogus-subcommand"]);
    assert.notEqual(bad.exitCode, 0);
    assert.ok(bad.stderr.length > 0);
  } finally { r.cleanup(); }
});

test("gitOk returns true for clean exit, false for nonzero", () => {
  const r = initRepo();
  try {
    assert.equal(gitOk(r.dir, ["rev-parse", "--is-inside-work-tree"]), true);
    assert.equal(gitOk(r.dir, ["bogus-subcommand"]), false);
  } finally { r.cleanup(); }
});
