import { test } from "node:test";
import { strict as assert } from "node:assert";
import { readFileSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { writeStatus } from "../src/transition-status-writer.mjs";

function tmp() {
  const d = mkdtempSync(join(tmpdir(), "uc-status-"));
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("writeStatus: includes command and timestamp on every write", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "s.json");
    writeStatus(p, "to-standard", { step: "staged", result: "in-progress" });
    const parsed = JSON.parse(readFileSync(p, "utf8"));
    assert.equal(parsed.command, "to-standard");
    assert.equal(parsed.step, "staged");
    assert.equal(parsed.result, "in-progress");
    assert.match(parsed.timestamp, /^\d{4}-\d{2}-\d{2}T/);
  } finally { t.cleanup(); }
});

test("writeStatus: rewrites file completely on each call", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "s.json");
    writeStatus(p, "to-standard", { step: "staged", result: "in-progress", error: "old" });
    writeStatus(p, "to-standard", { step: "complete", result: "ok" });
    const parsed = JSON.parse(readFileSync(p, "utf8"));
    assert.equal(parsed.step, "complete");
    assert.equal(parsed.result, "ok");
    assert.equal(parsed.error, undefined, "old fields must not leak");
  } finally { t.cleanup(); }
});

test("writeStatus: creates parent directory", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "nested", "s.json");
    writeStatus(p, "delete-from-ninja", { step: "deleting", result: "in-progress" });
    const parsed = JSON.parse(readFileSync(p, "utf8"));
    assert.equal(parsed.command, "delete-from-ninja");
  } finally { t.cleanup(); }
});
