import { test } from "node:test";
import { strict as assert } from "node:assert";
import {
  mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { writeMarker } from "../src/transition-marker.mjs";
import { finalizeTransition, resolveUnityBinary } from "../src/commands/finalize-transition.mjs";

function setup(kind) {
  const root = mkdtempSync(join(tmpdir(), "uc-fin-"));
  const libraryRoot = join(root, "Library", "UniClaude");
  const packagePath = join(root, "Packages", "com.arcforge.uniclaude");
  const statusPath = join(libraryRoot, "transition-status.json");
  const markerPath = join(libraryRoot, "pending-transition.json");

  mkdirSync(packagePath, { recursive: true });
  writeFileSync(join(packagePath, "dummy.txt"), "pretend this is a DLL");
  mkdirSync(libraryRoot, { recursive: true });

  writeMarker(markerPath, {
    kind,
    unityPid: 99999,
    unityAppPath: "/fake/Unity",
    projectPath: root,
    packagePath,
    statusPath,
    createdAt: new Date().toISOString(),
  });

  return {
    dir: root, libraryRoot, packagePath, statusPath, markerPath,
    cleanup: () => rmSync(root, { recursive: true, force: true }),
  };
}

test("finalize: happy path → complete", async () => {
  const s = setup("to-standard");
  try {
    let pollCount = 0;
    const spawned = [];

    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => pollCount++ < 2,  // alive for 2 polls, then dies
      sleep: async () => {},
      spawnUnity: (cmd, args) => spawned.push({ cmd, args }),
      timeoutMs: 60000,
    });

    assert.equal(existsSync(s.packagePath), false, "package deleted");
    const status = JSON.parse(readFileSync(s.statusPath, "utf8"));
    assert.equal(status.step, "complete");
    assert.equal(status.result, "ok");
    assert.equal(status.mode, "standard");
    assert.equal(spawned.length, 1);
    assert.equal(spawned[0].cmd, "/fake/Unity");
    assert.deepEqual(spawned[0].args, ["-projectPath", s.dir]);
  } finally { s.cleanup(); }
});

test("finalize: PID never dies → awaiting-exit error", async () => {
  const s = setup("to-standard");
  try {
    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => true,  // never dies
      sleep: async () => {},
      spawnUnity: () => { throw new Error("should not relaunch"); },
      timeoutMs: 1,  // instant timeout
    });

    const status = JSON.parse(readFileSync(s.statusPath, "utf8"));
    assert.equal(status.step, "awaiting-exit");
    assert.equal(status.result, "error");
    assert.match(status.error, /did not exit/);
    assert.equal(existsSync(s.packagePath), true, "package untouched on timeout");
  } finally { s.cleanup(); }
});

test("finalize: delete failure → deleting error", async () => {
  const s = setup("to-standard");
  try {
    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => false,
      sleep: async () => {},
      rmSync: () => { throw new Error("EBUSY: fake"); },
      spawnUnity: () => { throw new Error("should not relaunch"); },
      timeoutMs: 60000,
    });

    const status = JSON.parse(readFileSync(s.statusPath, "utf8"));
    assert.equal(status.step, "deleting");
    assert.equal(status.result, "error");
    assert.match(status.error, /EBUSY/);
  } finally { s.cleanup(); }
});

test("finalize: relaunch failure → complete with relaunchError", async () => {
  const s = setup("to-standard");
  try {
    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => false,
      sleep: async () => {},
      spawnUnity: () => { throw new Error("ENOENT: fake"); },
      timeoutMs: 60000,
    });

    const status = JSON.parse(readFileSync(s.statusPath, "utf8"));
    assert.equal(status.step, "complete");
    assert.equal(status.result, "ok", "conversion succeeded even if relaunch failed");
    assert.equal(status.mode, "standard");
    assert.match(status.relaunchError, /ENOENT/);
  } finally { s.cleanup(); }
});

test("finalize: delete-from-ninja kind → mode deleted", async () => {
  const s = setup("delete-from-ninja");
  try {
    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => false,
      sleep: async () => {},
      spawnUnity: () => {},
      timeoutMs: 60000,
    });

    const status = JSON.parse(readFileSync(s.statusPath, "utf8"));
    assert.equal(status.mode, "deleted");
    assert.equal(status.command, "delete-from-ninja");
  } finally { s.cleanup(); }
});

test("resolveUnityBinary: darwin .app bundle → inner Unity binary", () => {
  assert.equal(
    resolveUnityBinary("/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app", "darwin"),
    "/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity",
  );
});

test("resolveUnityBinary: win32 Unity.exe passes through", () => {
  assert.equal(
    resolveUnityBinary("C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.2f1\\Editor\\Unity.exe", "win32"),
    "C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.2f1\\Editor\\Unity.exe",
  );
});

test("resolveUnityBinary: linux binary passes through", () => {
  assert.equal(
    resolveUnityBinary("/opt/unity/6000.3.2f1/Editor/Unity", "linux"),
    "/opt/unity/6000.3.2f1/Editor/Unity",
  );
});

test("resolveUnityBinary: darwin path without .app suffix passes through", () => {
  assert.equal(
    resolveUnityBinary("/some/already/resolved/Unity", "darwin"),
    "/some/already/resolved/Unity",
  );
});

test("finalize: darwin .app bundle is translated before spawn", async () => {
  const s = setup("to-standard");
  try {
    writeMarker(s.markerPath, {
      kind: "to-standard",
      unityPid: 99999,
      unityAppPath: "/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app",
      projectPath: s.dir,
      packagePath: s.packagePath,
      statusPath: s.statusPath,
      createdAt: new Date().toISOString(),
    });

    const spawned = [];
    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => false,
      sleep: async () => {},
      spawnUnity: (cmd, args) => spawned.push({ cmd, args }),
      timeoutMs: 60000,
      platform: "darwin",
    });

    assert.equal(spawned.length, 1);
    assert.equal(
      spawned[0].cmd,
      "/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity",
    );
    assert.deepEqual(spawned[0].args, ["-projectPath", s.dir]);
  } finally { s.cleanup(); }
});

test("finalize: step progression is observable", async () => {
  const s = setup("to-standard");
  try {
    const steps = [];
    // Replace the real writeStatus with a spy by monkey-patching require... no,
    // we can observe step transitions via the final status alone. Instead,
    // assert the terminal state's step and that intermediate side-effects ran.
    // For progression testing, re-read status on each "sleep".

    let readAfterAwaiting = null;
    let readAfterDeleting = null;
    let poll = 0;

    await finalizeTransition({
      markerPath: s.markerPath,
      isProcessAlive: () => {
        if (poll === 0) {
          // On the first alive check, capture the status (should say awaiting-exit)
          readAfterAwaiting = JSON.parse(readFileSync(s.statusPath, "utf8"));
          poll++;
          return false;  // die immediately so we proceed to delete
        }
        return false;
      },
      sleep: async () => {},
      rmSync: (_p, _opts) => {
        // Capture the status at delete-time
        readAfterDeleting = JSON.parse(readFileSync(s.statusPath, "utf8"));
        rmSync(s.packagePath, { recursive: true, force: true });
      },
      spawnUnity: () => {},
      timeoutMs: 60000,
    });

    assert.equal(readAfterAwaiting.step, "awaiting-exit");
    assert.equal(readAfterDeleting.step, "deleting");
    const final = JSON.parse(readFileSync(s.statusPath, "utf8"));
    assert.equal(final.step, "complete");
  } finally { s.cleanup(); }
});
