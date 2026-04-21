import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { join, dirname } from "node:path";

const TARGET_PATH = "Packages/packages-lock.json";
const FILTER_NAME = "uniclaude";
const LINE = `${TARGET_PATH} filter=${FILTER_NAME}`;

function attrPath(projectRoot) {
  return join(projectRoot, ".git", "info", "attributes");
}

function readAttrs(path) {
  return existsSync(path) ? readFileSync(path, "utf8") : "";
}

export function hasFilterLine(projectRoot) {
  const text = readAttrs(attrPath(projectRoot));
  return text.split("\n").some(l => l.trim() === LINE);
}

export function addFilterLine(projectRoot) {
  const p = attrPath(projectRoot);
  const text = readAttrs(p);

  const conflicting = text.split("\n")
    .find(l => l.startsWith(TARGET_PATH) && /filter=\S+/.test(l) && !l.includes(`filter=${FILTER_NAME}`));
  if (conflicting) {
    throw new Error(`${attrPath(projectRoot)} already has a filter for ${TARGET_PATH}: ${conflicting.trim()}`);
  }

  if (hasFilterLine(projectRoot)) return;
  mkdirSync(dirname(p), { recursive: true });
  const prefix = text.length > 0 && !text.endsWith("\n") ? "\n" : "";
  writeFileSync(p, text + prefix + LINE + "\n", "utf8");
}

export function removeFilterLine(projectRoot) {
  const p = attrPath(projectRoot);
  if (!existsSync(p)) return;
  const kept = readFileSync(p, "utf8").split("\n").filter(l => l.trim() !== LINE);
  writeFileSync(p, kept.join("\n"), "utf8");
}
