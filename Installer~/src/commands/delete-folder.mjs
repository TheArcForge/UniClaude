import { rmSync, readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { setTimeout as delay } from "node:timers/promises";

function toggleTrailingNewline(text) {
  return text.endsWith("\n") ? text.replace(/\n$/, "") : text + "\n";
}

export async function deleteFolder({ path, touchManifest, waitMs, statusPath }) {
  if (waitMs > 0) await delay(waitMs);

  if (existsSync(path)) {
    rmSync(path, { recursive: true, force: true });
  }

  if (touchManifest && existsSync(touchManifest)) {
    const current = readFileSync(touchManifest, "utf8");
    writeFileSync(touchManifest, toggleTrailingNewline(current), "utf8");
  }

  if (statusPath) {
    mkdirSync(dirname(statusPath), { recursive: true });
    writeFileSync(statusPath, JSON.stringify({
      command: "delete-folder",
      result: "ok",
      step: "deletion-complete",
      timestamp: new Date().toISOString(),
    }, null, 2) + "\n", "utf8");
  }
}
