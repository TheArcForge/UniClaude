import { copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { spawn } from "node:child_process";
import { readManifest, writeManifest, addPackage } from "../manifest.mjs";
import { readFilterManifest } from "../filter-manifest.mjs";
import { removeSentinel } from "../exclude.mjs";
import { removeFilterLine } from "../attributes.mjs";
import { git } from "../git.mjs";

const UNICLAUDE_NAME = "com.arcforge.uniclaude";

function defaultSpawnDetached(cmd, args) {
  const child = spawn(cmd, args, { detached: true, stdio: "ignore" });
  child.unref();
}

function resolveRestoreSpec(libraryRoot, packagePath) {
  const fm = readFilterManifest(join(libraryRoot, "filter-manifest.json"));
  if (fm.originalSpec) return fm.originalSpec;

  const pkgJsonPath = join(packagePath, "package.json");
  if (!existsSync(pkgJsonPath)) {
    throw new Error(
      `cannot determine UniClaude dependency spec: no originalSpec in filter-manifest.json and missing ${pkgJsonPath}`
    );
  }
  const pkgJson = JSON.parse(readFileSync(pkgJsonPath, "utf8"));
  const gitUrl = pkgJson?.repository?.url;
  if (!gitUrl) throw new Error(`no repository.url in ${pkgJsonPath}`);
  return gitUrl;
}

export function toStandardPhase1({
  projectRoot,
  libraryRoot,
  installerSourcePath,
  spawnDetached = defaultSpawnDetached,
  nodeBinary = process.execPath,
  deleteWaitMs = 15000,
}) {
  const manifestPath = join(projectRoot, "Packages", "manifest.json");
  const packagePath = join(projectRoot, "Packages", UNICLAUDE_NAME);
  const persistentInstaller = join(libraryRoot, "installer-persistent.mjs");

  mkdirSync(libraryRoot, { recursive: true });
  if (existsSync(installerSourcePath)) {
    copyFileSync(installerSourcePath, persistentInstaller);
    const srcSource = join(dirname(installerSourcePath), "src");
    const srcDest = join(libraryRoot, "src");
    if (existsSync(srcSource)) {
      rmSync(srcDest, { recursive: true, force: true });
      cpSync(srcSource, srcDest, { recursive: true });
    }
  }
  if (!existsSync(persistentInstaller)) {
    throw new Error(`persistent installer missing: ${persistentInstaller}`);
  }

  const restoreSpec = resolveRestoreSpec(libraryRoot, packagePath);

  git(projectRoot, ["config", "--unset", "filter.uniclaude.clean"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.smudge"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.required"]);
  removeFilterLine(projectRoot);

  removeSentinel(projectRoot);

  const manifest = readManifest(manifestPath);
  addPackage(manifest, UNICLAUDE_NAME, restoreSpec);
  writeManifest(manifestPath, manifest);

  spawnDetached(nodeBinary, [
    persistentInstaller,
    "delete-folder",
    "--path", packagePath,
    "--touch-manifest", manifestPath,
    "--wait-ms", String(deleteWaitMs),
    "--status-path", join(libraryRoot, "transition-status.json"),
  ]);

  return { result: "ok", mode: "standard-pending" };
}
