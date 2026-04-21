import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawn } from "node:child_process";
import { readManifest, writeManifest, addPackage } from "../manifest.mjs";
import { removeSentinel } from "../exclude.mjs";
import { removeFilterLine } from "../attributes.mjs";
import { git } from "../git.mjs";

const UNICLAUDE_NAME = "com.arcforge.uniclaude";

function defaultSpawnDetached(cmd, args) {
  const child = spawn(cmd, args, { detached: true, stdio: "ignore" });
  child.unref();
}

export function toStandardPhase1({
  projectRoot,
  libraryRoot,
  installerSourcePath,
  spawnDetached = defaultSpawnDetached,
  deleteWaitMs = 15000,
}) {
  const manifestPath = join(projectRoot, "Packages", "manifest.json");
  const packagePath = join(projectRoot, "Packages", UNICLAUDE_NAME);
  const pkgJsonPath = join(packagePath, "package.json");
  const persistentInstaller = join(libraryRoot, "installer-persistent.mjs");

  mkdirSync(libraryRoot, { recursive: true });
  if (existsSync(installerSourcePath)) copyFileSync(installerSourcePath, persistentInstaller);
  if (!existsSync(persistentInstaller)) {
    throw new Error(`persistent installer missing: ${persistentInstaller}`);
  }

  if (!existsSync(pkgJsonPath)) throw new Error(`missing ${pkgJsonPath}`);
  const pkgJson = JSON.parse(readFileSync(pkgJsonPath, "utf8"));
  const gitUrl = pkgJson?.repository?.url;
  if (!gitUrl) throw new Error(`no repository.url in ${pkgJsonPath}`);

  git(projectRoot, ["config", "--unset", "filter.uniclaude.clean"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.smudge"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.required"]);
  removeFilterLine(projectRoot);

  removeSentinel(projectRoot);

  const manifest = readManifest(manifestPath);
  addPackage(manifest, UNICLAUDE_NAME, gitUrl);
  writeManifest(manifestPath, manifest);

  spawnDetached("node", [
    persistentInstaller,
    "delete-folder",
    "--path", packagePath,
    "--touch-manifest", manifestPath,
    "--wait-ms", String(deleteWaitMs),
    "--status-path", join(libraryRoot, "transition-status.json"),
  ]);

  return { result: "ok", mode: "standard-pending" };
}
