import { test } from "node:test";
import { strict as assert } from "node:assert";
import { addSentinel, removeSentinel, hasSentinel, SENTINEL_COMMENT } from "../src/exclude.mjs";
import { mkdtempSync, writeFileSync, readFileSync, rmSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

function tmpRepo() {
  const d = mkdtempSync(join(tmpdir(), "uc-excl-"));
  mkdirSync(join(d, ".git", "info"), { recursive: true });
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("addSentinel appends block to missing exclude file", () => {
  const r = tmpRepo();
  try {
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    const text = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    assert.match(text, /UniClaude ninja-mode/);
    assert.match(text, /Packages\/com\.arcforge\.uniclaude\//);
  } finally { r.cleanup(); }
});

test("addSentinel preserves existing content", () => {
  const r = tmpRepo();
  try {
    writeFileSync(join(r.dir, ".git", "info", "exclude"), "# existing\nsomefile.tmp\n");
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    const text = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    assert.match(text, /# existing/);
    assert.match(text, /somefile\.tmp/);
    assert.match(text, /UniClaude ninja-mode/);
  } finally { r.cleanup(); }
});

test("hasSentinel detects presence", () => {
  const r = tmpRepo();
  try {
    assert.equal(hasSentinel(r.dir), false);
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    assert.equal(hasSentinel(r.dir), true);
  } finally { r.cleanup(); }
});

test("removeSentinel strips only the sentinel block", () => {
  const r = tmpRepo();
  try {
    writeFileSync(join(r.dir, ".git", "info", "exclude"), "# existing\nsomefile.tmp\n");
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    removeSentinel(r.dir);
    const text = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    assert.equal(text, "# existing\nsomefile.tmp\n");
  } finally { r.cleanup(); }
});

test("addSentinel is idempotent", () => {
  const r = tmpRepo();
  try {
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    const text = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    const matches = text.match(/UniClaude ninja-mode/g) || [];
    assert.equal(matches.length, 1);
  } finally { r.cleanup(); }
});

test("removeSentinel handles CRLF line endings", () => {
  const r = tmpRepo();
  try {
    // Simulate a Windows-authored exclude file with CRLF, then add sentinel which uses LF
    writeFileSync(join(r.dir, ".git", "info", "exclude"), "# existing\r\nsomefile.tmp\r\n");
    addSentinel(r.dir, "Packages/com.arcforge.uniclaude/");
    removeSentinel(r.dir);
    const text = readFileSync(join(r.dir, ".git", "info", "exclude"), "utf8");
    // Original content's CRLF line endings are preserved on kept lines
    assert.equal(text, "# existing\r\nsomefile.tmp\r\n");
  } finally { r.cleanup(); }
});
