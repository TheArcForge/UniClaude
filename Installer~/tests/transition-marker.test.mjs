import { test } from "node:test";
import { strict as assert } from "node:assert";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { writeMarker, readMarker } from "../src/transition-marker.mjs";

function tmp() {
  const d = mkdtempSync(join(tmpdir(), "uc-marker-"));
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("marker: round-trip all fields", () => {
  const t = tmp();
  try {
    const path = join(t.dir, "pending-transition.json");
    const original = {
      kind: "to-standard",
      unityPid: 12345,
      unityAppPath: "/Applications/Unity.app/Contents/MacOS/Unity",
      projectPath: "/Users/alice/proj",
      packagePath: "/Users/alice/proj/Packages/com.arcforge.uniclaude",
      statusPath: "/Users/alice/proj/Library/UniClaude/transition-status.json",
      createdAt: "2026-04-22T18:30:00.000Z",
    };
    writeMarker(path, original);
    assert.deepEqual(readMarker(path), original);
  } finally { t.cleanup(); }
});

test("marker: readMarker throws when file missing", () => {
  const t = tmp();
  try {
    assert.throws(
      () => readMarker(join(t.dir, "nope.json")),
      /pending-transition\.json/
    );
  } finally { t.cleanup(); }
});

test("marker: writeMarker creates parent directory", () => {
  const t = tmp();
  try {
    const path = join(t.dir, "nested", "dir", "pending-transition.json");
    writeMarker(path, {
      kind: "delete-from-ninja",
      unityPid: 1,
      unityAppPath: "/x",
      projectPath: "/y",
      packagePath: "/y/Packages/com.arcforge.uniclaude",
      statusPath: "/y/Library/UniClaude/transition-status.json",
      createdAt: "2026-04-22T00:00:00.000Z",
    });
    const back = readMarker(path);
    assert.equal(back.kind, "delete-from-ninja");
  } finally { t.cleanup(); }
});
