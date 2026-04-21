import { test } from "node:test";
import { strict as assert } from "node:assert";
import { deleteFolder } from "../src/commands/delete-folder.mjs";
import {
  mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

function setup() {
  const d = mkdtempSync(join(tmpdir(), "uc-del-"));
  mkdirSync(join(d, "target"), { recursive: true });
  writeFileSync(join(d, "target", "file.txt"), "hi");
  writeFileSync(join(d, "manifest.json"), '{\n  "dependencies": {}\n}\n');
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("deleteFolder removes directory and toggles manifest trailing newline", async () => {
  const r = setup();
  try {
    const manifestPath = join(r.dir, "manifest.json");
    const before = readFileSync(manifestPath, "utf8");

    await deleteFolder({
      path: join(r.dir, "target"),
      touchManifest: manifestPath,
      waitMs: 10,
    });

    assert.equal(existsSync(join(r.dir, "target")), false);

    const after = readFileSync(manifestPath, "utf8");
    assert.notEqual(after, before, "manifest content must change to trigger Unity watcher");
    assert.ok(
      after === before + "\n" || before === after + "\n" ||
      after === before.replace(/\n$/, "") || before === after.replace(/\n$/, ""),
      "expected single-newline toggle");
  } finally { r.cleanup(); }
});

test("deleteFolder is safe when target already absent", async () => {
  const r = setup();
  try {
    rmSync(join(r.dir, "target"), { recursive: true });
    await deleteFolder({
      path: join(r.dir, "target"),
      touchManifest: join(r.dir, "manifest.json"),
      waitMs: 10,
    });
  } finally { r.cleanup(); }
});

test("deleteFolder writes completion status when statusPath given", async () => {
  const r = setup();
  try {
    const statusPath = join(r.dir, "status.json");
    await deleteFolder({
      path: join(r.dir, "target"),
      touchManifest: join(r.dir, "manifest.json"),
      waitMs: 10,
      statusPath,
    });
    assert.ok(existsSync(statusPath));
    const parsed = JSON.parse(readFileSync(statusPath, "utf8"));
    assert.equal(parsed.step, "deletion-complete");
  } finally { r.cleanup(); }
});
