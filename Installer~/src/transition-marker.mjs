import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";

/**
 * Write a pending-transition.json marker. Creates parent directories as needed.
 * The marker is consumed by finalize-transition.mjs and by Unity's post-restart
 * resume logic.
 */
export function writeMarker(path, marker) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, JSON.stringify(marker, null, 2) + "\n", "utf8");
}

/**
 * Read a pending-transition.json marker. Throws if the file is missing so the
 * helper fails loudly rather than operating on partial state.
 */
export function readMarker(path) {
  if (!existsSync(path)) {
    throw new Error(`pending-transition.json missing: ${path}`);
  }
  return JSON.parse(readFileSync(path, "utf8"));
}
