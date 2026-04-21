import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";

export function readFilterManifest(path) {
  if (!existsSync(path)) return { owned: {} };
  const parsed = JSON.parse(readFileSync(path, "utf8"));
  return { owned: parsed.owned || {} };
}

export function writeFilterManifest(path, owned) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, JSON.stringify({ owned }, null, 2) + "\n", "utf8");
}
