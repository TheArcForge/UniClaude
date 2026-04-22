import { mkdirSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";

/**
 * Overwrite transition-status.json with a fresh payload. Each call produces a
 * complete object — callers should pass everything they want persisted.
 *
 * @param {string} path - Absolute path to the status file.
 * @param {string} command - The transition command name (e.g. "to-standard").
 * @param {object} patch - Fields to include in the payload.
 */
export function writeStatus(path, command, patch) {
  mkdirSync(dirname(path), { recursive: true });
  const payload = {
    command,
    ...patch,
    timestamp: new Date().toISOString(),
  };
  writeFileSync(path, JSON.stringify(payload, null, 2) + "\n", "utf8");
}
