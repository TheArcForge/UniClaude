import { copyFileSync, cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { removeSentinel } from "../exclude.mjs";
import { removeFilterLine } from "../attributes.mjs";
import { git } from "../git.mjs";
import { writeMarker } from "../transition-marker.mjs";
import { writeStatus } from "../transition-status-writer.mjs";

const UNICLAUDE_NAME = "com.arcforge.uniclaude";

export function deleteFromNinja({
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

  git(projectRoot, ["config", "--unset", "filter.uniclaude.clean"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.smudge"]);
  git(projectRoot, ["config", "--unset", "filter.uniclaude.required"]);
  removeFilterLine(projectRoot);
  removeSentinel(projectRoot);

  writeMarker(markerPath, {
    kind: "delete-from-ninja",
    unityPid,
    unityAppPath,
    projectPath: projectRoot,
    packagePath,
    statusPath,
    createdAt: new Date().toISOString(),
  });
  writeStatus(statusPath, "delete-from-ninja", { step: "staged", result: "in-progress" });

  return { result: "ok", mode: "delete-pending", markerPath };
}
