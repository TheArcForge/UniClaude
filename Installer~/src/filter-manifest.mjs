import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";

export function readFilterManifest(path) {
  if (!existsSync(path)) return { owned: {}, originalSpec: null };
  const parsed = JSON.parse(readFileSync(path, "utf8"));
  return {
    owned: parsed.owned || {},
    originalSpec: parsed.originalSpec || null,
  };
}

export function writeFilterManifest(path, owned, originalSpec = null) {
  mkdirSync(dirname(path), { recursive: true });
  const payload = originalSpec ? { owned, originalSpec } : { owned };
  writeFileSync(path, JSON.stringify(payload, null, 2) + "\n", "utf8");
}
