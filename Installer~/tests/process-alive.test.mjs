import { test } from "node:test";
import { strict as assert } from "node:assert";
import { spawn } from "node:child_process";
import { isProcessAlive } from "../src/process-alive.mjs";

test("isProcessAlive: own process is alive", () => {
  assert.equal(isProcessAlive(process.pid), true);
});

test("isProcessAlive: child process tracked accurately", async () => {
  const child = spawn(process.execPath, ["-e", "setTimeout(()=>{},5000)"], {
    stdio: "ignore",
  });
  try {
    assert.equal(isProcessAlive(child.pid), true, "alive while running");
    child.kill("SIGKILL");
    await new Promise(resolve => child.once("exit", resolve));
    // On POSIX, a defunct zombie can briefly look "alive" until reaped by waitpid.
    // The `exit` event fires after reap, so this should be reliable.
    assert.equal(isProcessAlive(child.pid), false, "dead after exit");
  } finally {
    if (!child.killed) child.kill("SIGKILL");
  }
});

test("isProcessAlive: bogus PID is dead", () => {
  // Use an obviously-absent PID. 2^31-1 is never assigned on real systems.
  assert.equal(isProcessAlive(2147483646), false);
});
