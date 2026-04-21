import { test } from "node:test";
import { strict as assert } from "node:assert";
import { readFilterManifest, writeFilterManifest } from "../src/filter-manifest.mjs";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

function tmp() {
  const d = mkdtempSync(join(tmpdir(), "uc-fm-"));
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("round-trip", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "filter-manifest.json");
    const owned = { "com.arcforge.uniclaude": { version: "x", dependencies: {} } };
    writeFilterManifest(p, owned);
    assert.deepEqual(readFilterManifest(p).owned, owned);
  } finally { t.cleanup(); }
});

test("readFilterManifest returns empty owned when file absent", () => {
  const t = tmp();
  try {
    const r = readFilterManifest(join(t.dir, "absent.json"));
    assert.deepEqual(r.owned, {});
  } finally { t.cleanup(); }
});
