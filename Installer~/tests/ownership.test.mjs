import { test } from "node:test";
import { strict as assert } from "node:assert";
import { computeOwnership } from "../src/ownership.mjs";

test("UniClaude is always in the ownership set", () => {
  const manifest = { dependencies: { "com.arcforge.uniclaude": "..." } };
  const lock = {
    dependencies: {
      "com.arcforge.uniclaude": { version: "...", dependencies: {} },
    },
  };
  const owned = computeOwnership(manifest, lock, "com.arcforge.uniclaude");
  assert.deepEqual(Object.keys(owned).sort(), ["com.arcforge.uniclaude"]);
});

test("dep reachable only through UniClaude is owned", () => {
  const manifest = { dependencies: { "com.arcforge.uniclaude": "..." } };
  const lock = {
    dependencies: {
      "com.arcforge.uniclaude": {
        version: "...",
        dependencies: { "com.unity.nuget.newtonsoft-json": "3.2.1" },
      },
      "com.unity.nuget.newtonsoft-json": { version: "3.2.1", dependencies: {} },
    },
  };
  const owned = computeOwnership(manifest, lock, "com.arcforge.uniclaude");
  assert.deepEqual(Object.keys(owned).sort(), [
    "com.arcforge.uniclaude",
    "com.unity.nuget.newtonsoft-json",
  ]);
});

test("dep shared with another top-level is NOT owned", () => {
  const manifest = {
    dependencies: {
      "com.arcforge.uniclaude": "...",
      "com.other.package": "1.0",
    },
  };
  const lock = {
    dependencies: {
      "com.arcforge.uniclaude": {
        version: "...",
        dependencies: { "com.unity.nuget.newtonsoft-json": "3.2.1" },
      },
      "com.other.package": {
        version: "1.0",
        dependencies: { "com.unity.nuget.newtonsoft-json": "3.2.1" },
      },
      "com.unity.nuget.newtonsoft-json": { version: "3.2.1", dependencies: {} },
    },
  };
  const owned = computeOwnership(manifest, lock, "com.arcforge.uniclaude");
  assert.deepEqual(Object.keys(owned).sort(), ["com.arcforge.uniclaude"]);
});

test("transitive UniClaude-only deps are owned (two hops)", () => {
  const manifest = { dependencies: { "com.arcforge.uniclaude": "..." } };
  const lock = {
    dependencies: {
      "com.arcforge.uniclaude": {
        version: "...",
        dependencies: { "dep.one": "1.0" },
      },
      "dep.one": { version: "1.0", dependencies: { "dep.two": "1.0" } },
      "dep.two": { version: "1.0", dependencies: {} },
    },
  };
  const owned = computeOwnership(manifest, lock, "com.arcforge.uniclaude");
  assert.deepEqual(Object.keys(owned).sort(), [
    "com.arcforge.uniclaude",
    "dep.one",
    "dep.two",
  ]);
});

test("missing lock entry for manifest root is tolerated", () => {
  const manifest = {
    dependencies: {
      "com.arcforge.uniclaude": "...",
      "com.orphan": "1.0",
    },
  };
  const lock = {
    dependencies: {
      "com.arcforge.uniclaude": { version: "...", dependencies: {} },
    },
  };
  const owned = computeOwnership(manifest, lock, "com.arcforge.uniclaude");
  assert.deepEqual(Object.keys(owned).sort(), ["com.arcforge.uniclaude"]);
});
