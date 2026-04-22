import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { join, dirname } from "node:path";

export const SENTINEL_COMMENT = "# UniClaude ninja-mode (managed by UniClaude — do not edit)";
const BLOCK_START = SENTINEL_COMMENT;

function excludePath(projectRoot) {
  return join(projectRoot, ".git", "info", "exclude");
}

function readExclude(path) {
  return existsSync(path) ? readFileSync(path, "utf8") : "";
}

export function hasSentinel(projectRoot) {
  return readExclude(excludePath(projectRoot)).includes(BLOCK_START);
}

export function addSentinel(projectRoot, relativePath) {
  const p = excludePath(projectRoot);
  if (hasSentinel(projectRoot)) return;
  mkdirSync(dirname(p), { recursive: true });
  const existing = readExclude(p);
  const prefix = existing.length > 0 && !existing.endsWith("\n") ? "\n" : "";
  const block = `${BLOCK_START}\n${relativePath}\n`;
  writeFileSync(p, existing + prefix + block, "utf8");
}

export function removeSentinel(projectRoot) {
  const p = excludePath(projectRoot);
  if (!existsSync(p)) return;
  const lines = readFileSync(p, "utf8").split("\n");
  const out = [];
  let inBlock = false;
  for (const line of lines) {
    const stripped = line.endsWith("\r") ? line.slice(0, -1) : line;
    if (stripped === BLOCK_START) { inBlock = true; continue; }
    if (inBlock) {
      // block consists of the comment line + exactly one path line
      inBlock = false;
      continue;
    }
    out.push(line);
  }
  writeFileSync(p, out.join("\n"), "utf8");
}
