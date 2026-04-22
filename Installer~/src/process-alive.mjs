import { spawnSync } from "node:child_process";

/**
 * Returns true if the given PID belongs to a running process.
 * Cross-platform: POSIX uses the kill-zero trick; Windows shells out to tasklist
 * with CSV output so localized system messages don't affect detection.
 */
export function isProcessAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;

  if (process.platform === "win32") {
    const r = spawnSync(
      "tasklist",
      ["/FI", `PID eq ${pid}`, "/NH", "/FO", "CSV"],
      { encoding: "utf8" }
    );
    if (r.status !== 0) return false;
    // CSV row starts with a quoted image name. If no process matches, stdout
    // is either empty or just an INFO message. A matching CSV row must start
    // with a double-quote.
    return /^"/.test((r.stdout || "").trim());
  }

  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    // EPERM means the process exists but we lack permission — treat as alive.
    if (err && err.code === "EPERM") return true;
    return false;
  }
}
