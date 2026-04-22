import { test } from "node:test";
import { strict as assert } from "node:assert";
import { addFilterLine, removeFilterLine, hasFilterLine } from "../src/attributes.mjs";
import { mkdtempSync, writeFileSync, readFileSync, rmSync, mkdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

function tmpRepo() {
  const d = mkdtempSync(join(tmpdir(), "uc-attr-"));
  mkdirSync(join(d, ".git", "info"), { recursive: true });
  return { dir: d, cleanup: () => rmSync(d, { recursive: true, force: true }) };
}

test("addFilterLine creates file and writes filter", () => {
  const r = tmpRepo();
  try {
    addFilterLine(r.dir);
    const text = readFileSync(join(r.dir, ".git", "info", "attributes"), "utf8");
    assert.match(text, /Packages\/packages-lock\.json filter=uniclaude/);
  } finally { r.cleanup(); }
});

test("hasFilterLine detects presence", () => {
  const r = tmpRepo();
  try {
    assert.equal(hasFilterLine(r.dir), false);
    addFilterLine(r.dir);
    assert.equal(hasFilterLine(r.dir), true);
  } finally { r.cleanup(); }
});

test("addFilterLine is idempotent", () => {
  const r = tmpRepo();
  try {
    addFilterLine(r.dir);
    addFilterLine(r.dir);
    const matches = (readFileSync(join(r.dir, ".git", "info", "attributes"), "utf8")
      .match(/filter=uniclaude/g) || []).length;
    assert.equal(matches, 1);
  } finally { r.cleanup(); }
});

test("removeFilterLine strips only that line", () => {
  const r = tmpRepo();
  try {
    writeFileSync(join(r.dir, ".git", "info", "attributes"), "*.txt text\n");
    addFilterLine(r.dir);
    removeFilterLine(r.dir);
    const text = readFileSync(join(r.dir, ".git", "info", "attributes"), "utf8");
    assert.equal(text.trim(), "*.txt text");
  } finally { r.cleanup(); }
});

test("throws on existing filter= line for packages-lock.json by someone else", () => {
  const r = tmpRepo();
  try {
    writeFileSync(join(r.dir, ".git", "info", "attributes"),
      "Packages/packages-lock.json filter=other\n");
    assert.throws(() => addFilterLine(r.dir), /already has.*filter/i);
  } finally { r.cleanup(); }
});
