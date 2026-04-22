import { test } from "node:test";
import { strict as assert } from "node:assert";
import { readManifest, writeManifest, removePackage, addPackage } from "../src/manifest.mjs";
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

function tmp() {
  const d = mkdtempSync(join(tmpdir(), "uc-manifest-"));
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("readManifest returns parsed JSON", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "manifest.json");
    writeFileSync(p, '{\n  "dependencies": {\n    "foo": "1.0.0"\n  }\n}\n');
    const m = readManifest(p);
    assert.deepEqual(m.dependencies, { foo: "1.0.0" });
  } finally { t.cleanup(); }
});

test("writeManifest produces 2-space indent with trailing newline", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "manifest.json");
    writeManifest(p, { dependencies: { foo: "1.0.0" } });
    const text = readFileSync(p, "utf8");
    assert.equal(text, '{\n  "dependencies": {\n    "foo": "1.0.0"\n  }\n}\n');
  } finally { t.cleanup(); }
});

test("removePackage removes key", () => {
  const m = { dependencies: { "com.arcforge.uniclaude": "git+...", other: "1.0" } };
  removePackage(m, "com.arcforge.uniclaude");
  assert.deepEqual(m.dependencies, { other: "1.0" });
});

test("removePackage is no-op when key absent", () => {
  const m = { dependencies: { other: "1.0" } };
  removePackage(m, "com.arcforge.uniclaude");
  assert.deepEqual(m.dependencies, { other: "1.0" });
});

test("addPackage inserts key", () => {
  const m = { dependencies: { other: "1.0" } };
  addPackage(m, "com.arcforge.uniclaude", "https://github.com/TheArcForge/UniClaude.git");
  assert.equal(m.dependencies["com.arcforge.uniclaude"], "https://github.com/TheArcForge/UniClaude.git");
});

test("addPackage creates dependencies object if missing", () => {
  const m = {};
  addPackage(m, "foo", "1.0");
  assert.deepEqual(m.dependencies, { foo: "1.0" });
});
