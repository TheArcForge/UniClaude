import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { spawn } from "node:child_process";
import { removeSentinel } from "../exclude.mjs";
import { removeFilterLine } from "../attributes.mjs";
import { git } from "../git.mjs";

const UNICLAUDE_NAME = "com.arcforge.uniclaude";

function defaultSpawnDetached(cmd, args) {
  const child = spawn(cmd, args, { detached: true, stdio: "ignore" });
  child.unref();
}

export function deleteFromNinja({
  projectRoot,
  libraryRoot,
  installerSourcePath,
  spawnDetached = defaultSpawnDetached,
  nodeBinary = process.execPath,
  deleteWaitMs = 15000,
}) {
  const packagePath = join(projectRoot, "Packages", UNICLAUDE_NAME);
  const manifestPath = join(projectRoot, "Packages", "manifest.json");
  const persistentInstaller = join(libraryRoot, "installer-persistent.mjs");

  mkdirSync(libraryRoot, { recursive: true });
  if (existsSync(installerSourcePath)) copyFileSync(installerSourcePath, persistentInstaller);
  if (!existsSync(persistentInstaller)) {
    throw new Error(`persistent installer missing: ${persistentInstaller}`);
  }

  git(projectRoot, ["config", "--unset", "filter.uniclaude.clean"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.smudge"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.required"]);
  removeFilterLine(projectRoot);
  removeSentinel(projectRoot);

  spawnDetached(nodeBinary, [
    persistentInstaller,
    "delete-folder",
    "--path", packagePath,
    "--touch-manifest", manifestPath,
    "--wait-ms", String(deleteWaitMs),
    "--status-path", join(libraryRoot, "transition-status.json"),
  ]);

  return { result: "ok", mode: "deleted" };
}
