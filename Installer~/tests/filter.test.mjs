import { test } from "node:test";
import { strict as assert } from "node:assert";
import { cleanLockJson, smudgeLockJson } from "../src/filter.mjs";

const OWNED = {
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
};

const TEAM_JSON = JSON.stringify({
  dependencies: {
    "com.unity.ugui": { version: "2.0.0", source: "builtin", dependencies: {} },
  },
}, null, 2) + "\n";

function workingJson() {
  return JSON.stringify({
    dependencies: {
      "com.arcforge.uniclaude": OWNED["com.arcforge.uniclaude"],
      "com.unity.nuget.newtonsoft-json": OWNED["com.unity.nuget.newtonsoft-json"],
      "com.unity.ugui": { version: "2.0.0", source: "builtin", dependencies: {} },
    },
  }, null, 2) + "\n";
}

test("clean strips owned entries", () => {
  const cleaned = cleanLockJson(workingJson(), OWNED);
  assert.equal(cleaned, TEAM_JSON);
});

test("smudge inserts owned entries", () => {
  const smudged = smudgeLockJson(TEAM_JSON, OWNED);
  assert.equal(smudged, workingJson());
});

test("round-trip: clean(smudge(team)) === team", () => {
  assert.equal(cleanLockJson(smudgeLockJson(TEAM_JSON, OWNED), OWNED), TEAM_JSON);
});

test("round-trip: smudge(clean(working)) === working", () => {
  const w = workingJson();
  assert.equal(smudgeLockJson(cleanLockJson(w, OWNED), OWNED), w);
});

test("clean is idempotent when nothing to strip", () => {
  assert.equal(cleanLockJson(TEAM_JSON, OWNED), TEAM_JSON);
});

test("smudge is idempotent when entries already present", () => {
  const w = workingJson();
  assert.equal(smudgeLockJson(w, OWNED), w);
});
