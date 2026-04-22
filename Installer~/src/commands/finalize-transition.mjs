import { rmSync as realRmSync, existsSync } from "node:fs";
import { spawn } from "node:child_process";
import { setTimeout as defaultSleep } from "node:timers/promises";
import { readMarker } from "../transition-marker.mjs";
import { writeStatus } from "../transition-status-writer.mjs";
import { isProcessAlive as realIsProcessAlive } from "../process-alive.mjs";

const DEFAULT_TIMEOUT_MS = 30000;
const POLL_INTERVAL_MS = 250;

/**
 * Map a transition kind to the mode string written into terminal status.
 *
 * @param {string} kind - The marker kind (e.g. "to-standard", "delete-from-ninja").
 * @returns {string} The mode label for the status payload.
 */
function modeFor(kind) {
  // Marker kind strings mirror the command names used elsewhere in the installer.
  // "delete-from-ninja" is the full command name for removal; its resulting mode is "deleted".
  return kind === "to-standard" ? "standard" : "deleted";
}

/**
 * Default Unity relaunch implementation: spawn detached so the child outlives
 * this process, then unref so we don't keep the event loop alive.
 *
 * @param {string} cmd - Absolute path to the Unity executable.
 * @param {string[]} args - Arguments to pass (e.g. ["-projectPath", "/..."]).
 */
function defaultSpawnUnity(cmd, args) {
  const child = spawn(cmd, args, { detached: true, stdio: "ignore" });
  child.unref();
}

/**
 * Finalize a staged transition: wait for Unity to exit, delete the package
 * folder, relaunch Unity, write terminal status. All external effects are
 * injectable so tests never spawn real processes.
 *
 * @param {object} opts
 * @param {string} opts.markerPath - Path to the pending-transition.json marker.
 * @param {function(number): boolean} opts.isProcessAlive - Returns true if the PID is still running.
 * @param {function(number): Promise<void>} opts.sleep - Async delay; injected in tests as a no-op.
 * @param {function(string, object): void} opts.rmSync - Delete a path recursively; injectable for error simulation.
 * @param {function(string, string[]): void} opts.spawnUnity - Launch Unity; injectable for error simulation.
 * @param {number} opts.timeoutMs - Maximum ms to wait for Unity to exit before giving up.
 */
export async function finalizeTransition({
  markerPath,
  isProcessAlive = realIsProcessAlive,
  sleep = (ms) => defaultSleep(ms),
  rmSync = realRmSync,
  spawnUnity = defaultSpawnUnity,
  timeoutMs = DEFAULT_TIMEOUT_MS,
}) {
  const marker = readMarker(markerPath);
  const write = (patch) => writeStatus(marker.statusPath, marker.kind, patch);

  write({ step: "awaiting-exit", result: "in-progress" });

  const deadline = Date.now() + timeoutMs;
  while (isProcessAlive(marker.unityPid)) {
    if (Date.now() >= deadline) {
      write({
        step: "awaiting-exit",
        result: "error",
        error: `Unity did not exit within ${timeoutMs}ms`,
      });
      return;
    }
    await sleep(POLL_INTERVAL_MS);
  }

  write({ step: "deleting", result: "in-progress" });
  try {
    if (existsSync(marker.packagePath)) {
      rmSync(marker.packagePath, { recursive: true, force: true });
    }
  } catch (err) {
    write({ step: "deleting", result: "error", error: String(err && err.message || err) });
    return;
  }

  write({ step: "relaunching", result: "in-progress" });
  try {
    spawnUnity(marker.unityAppPath, ["-projectPath", marker.projectPath]);
  } catch (err) {
    write({
      step: "complete",
      result: "ok",
      mode: modeFor(marker.kind),
      relaunchError: String(err && err.message || err),
    });
    return;
  }

  write({ step: "complete", result: "ok", mode: modeFor(marker.kind) });
}
