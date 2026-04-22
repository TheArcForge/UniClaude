import { copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { readManifest, writeManifest, addPackage } from "../manifest.mjs";
import { readFilterManifest } from "../filter-manifest.mjs";
import { removeSentinel } from "../exclude.mjs";
import { removeFilterLine } from "../attributes.mjs";
import { git } from "../git.mjs";
import { writeMarker } from "../transition-marker.mjs";
import { writeStatus } from "../transition-status-writer.mjs";

const UNICLAUDE_NAME = "com.arcforge.uniclaude";

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
  unityPid,
  unityAppPath,
}) {
  if (!Number.isInteger(unityPid) || unityPid <= 0) {
    throw new Error("unityPid required (positive integer)");
  }
  if (!unityAppPath || typeof unityAppPath !== "string") {
    throw new Error("unityAppPath required");
  }

  const manifestPath = join(projectRoot, "Packages", "manifest.json");
  const packagePath = join(projectRoot, "Packages", UNICLAUDE_NAME);
  const persistentInstaller = join(libraryRoot, "installer-persistent.mjs");
  const markerPath = join(libraryRoot, "pending-transition.json");
  const statusPath = join(libraryRoot, "transition-status.json");

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

  writeMarker(markerPath, {
    kind: "to-standard",
    unityPid,
    unityAppPath,
    projectPath: projectRoot,
    packagePath,
    statusPath,
    createdAt: new Date().toISOString(),
  });
  writeStatus(statusPath, "to-standard", { step: "staged", result: "in-progress" });

  return { result: "ok", mode: "standard-pending", markerPath };
}
