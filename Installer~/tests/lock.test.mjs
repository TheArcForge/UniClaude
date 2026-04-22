import { test } from "node:test";
import { strict as assert } from "node:assert";
import {
  readLock, writeLock, stripEntries, insertEntries, hasEntry,
} from "../src/lock.mjs";
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

function tmp() {
  const d = mkdtempSync(join(tmpdir(), "uc-lock-"));
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

const FIXTURE = {
  dependencies: {
    "com.arcforge.uniclaude": {
      version: "https://github.com/TheArcForge/UniClaude.git",
      depth: 0,
      source: "git",
      dependencies: { "com.unity.nuget.newtonsoft-json": "3.2.1" },
      hash: "abc123",
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
};

test("readLock/writeLock round-trip is byte-identical", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "packages-lock.json");
    writeLock(p, FIXTURE);
    const once = readFileSync(p, "utf8");
    writeLock(p, readLock(p));
    const twice = readFileSync(p, "utf8");
    assert.equal(once, twice, "round-trip must be byte-identical");
  } finally { t.cleanup(); }
});

test("writeLock uses 2-space indent and trailing newline", () => {
  const t = tmp();
  try {
    const p = join(t.dir, "packages-lock.json");
    writeLock(p, { dependencies: {} });
    const text = readFileSync(p, "utf8");
    assert.equal(text, '{\n  "dependencies": {}\n}\n');
  } finally { t.cleanup(); }
});

test("stripEntries removes named keys from .dependencies", () => {
  const lock = structuredClone(FIXTURE);
  stripEntries(lock, ["com.arcforge.uniclaude", "com.unity.nuget.newtonsoft-json"]);
  assert.deepEqual(Object.keys(lock.dependencies), ["com.unity.ugui"]);
});

test("stripEntries is no-op for keys not present", () => {
  const lock = structuredClone(FIXTURE);
  stripEntries(lock, ["com.not.here"]);
  assert.equal(Object.keys(lock.dependencies).length, 3);
});

test("insertEntries inserts entries sorted alphabetically among existing keys", () => {
  const lock = { dependencies: { "com.unity.ugui": FIXTURE.dependencies["com.unity.ugui"] } };
  insertEntries(lock, {
    "com.arcforge.uniclaude": FIXTURE.dependencies["com.arcforge.uniclaude"],
    "com.unity.nuget.newtonsoft-json": FIXTURE.dependencies["com.unity.nuget.newtonsoft-json"],
  });
  assert.deepEqual(Object.keys(lock.dependencies).sort(), [
    "com.arcforge.uniclaude",
    "com.unity.nuget.newtonsoft-json",
    "com.unity.ugui",
  ]);
});

test("insertEntries produces sorted key order in output", () => {
  const lock = { dependencies: {} };
  insertEntries(lock, {
    "z.pkg": { version: "1" },
    "a.pkg": { version: "1" },
    "m.pkg": { version: "1" },
  });
  assert.deepEqual(Object.keys(lock.dependencies), ["a.pkg", "m.pkg", "z.pkg"]);
});

test("hasEntry returns boolean", () => {
  assert.equal(hasEntry(FIXTURE, "com.arcforge.uniclaude"), true);
  assert.equal(hasEntry(FIXTURE, "nope"), false);
});
