import { mkdirSync, copyFileSync, existsSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { readManifest, writeManifest, removePackage } from "../manifest.mjs";
import { readLock } from "../lock.mjs";
import { computeOwnership } from "../ownership.mjs";
import { writeFilterManifest } from "../filter-manifest.mjs";
import { addSentinel } from "../exclude.mjs";
import { addFilterLine } from "../attributes.mjs";
import { git } from "../git.mjs";
import { parseUpmUrl } from "../upm-url.mjs";

const UNICLAUDE_NAME = "com.arcforge.uniclaude";
const PACKAGE_DIR = `Packages/${UNICLAUDE_NAME}`;

function defaultClone(upmUrl, dest) {
  const { url, ref } = parseUpmUrl(upmUrl);

  const tryArgs = ref
    ? ["clone", "--quiet", "--branch", ref, url, dest]
    : ["clone", "--quiet", url, dest];
  const r = spawnSync("git", tryArgs, { encoding: "utf8" });
  if (r.status === 0) return;

  // --branch only accepts branch or tag names. If ref is a commit SHA, retry
  // with a plain clone followed by checkout.
  if (ref) {
    const r2 = spawnSync("git", ["clone", "--quiet", url, dest], { encoding: "utf8" });
    if (r2.status !== 0) throw new Error(`git clone failed: ${r2.stderr}`);
    const r3 = spawnSync("git", ["checkout", "--quiet", ref], { cwd: dest, encoding: "utf8" });
    if (r3.status !== 0) throw new Error(`git checkout ${ref} failed: ${r3.stderr}`);
    return;
  }
  throw new Error(`git clone failed: ${r.stderr}`);
}

export function toNinja({
  projectRoot,
  gitUrl,
  cloneFn = defaultClone,
  libraryRoot,
  installerSourcePath,
  nodeBinary = process.execPath,
}) {
  const manifestPath = join(projectRoot, "Packages", "manifest.json");
  const lockPath = join(projectRoot, "Packages", "packages-lock.json");
  const packagePath = join(projectRoot, PACKAGE_DIR);
  const persistentInstaller = join(libraryRoot, "installer-persistent.mjs");

  if (!existsSync(manifestPath)) throw new Error(`missing Packages/manifest.json`);
  if (!existsSync(lockPath)) throw new Error(`missing Packages/packages-lock.json`);

  const manifest = readManifest(manifestPath);
  const lock = readLock(lockPath);
  const owned = computeOwnership(manifest, lock, UNICLAUDE_NAME);
  const originalSpec = manifest?.dependencies?.[UNICLAUDE_NAME] || null;
  mkdirSync(libraryRoot, { recursive: true });
  writeFilterManifest(join(libraryRoot, "filter-manifest.json"), owned, originalSpec);

  cloneFn(gitUrl, packagePath);

  addSentinel(projectRoot, `${PACKAGE_DIR}/`);

  if (existsSync(installerSourcePath)) {
    copyFileSync(installerSourcePath, persistentInstaller);
  } else {
    writeFileSync(persistentInstaller, "// installer placeholder\n");
  }

  addFilterLine(projectRoot);
  const persistentForwardSlash = persistentInstaller.split("\\").join("/");
  const nodeForwardSlash = nodeBinary.split("\\").join("/");
  git(projectRoot, ["config", "filter.uniclaude.clean",  `"${nodeForwardSlash}" "${persistentForwardSlash}" clean`]);
  git(projectRoot, ["config", "filter.uniclaude.smudge", `"${nodeForwardSlash}" "${persistentForwardSlash}" smudge`]);
  git(projectRoot, ["config", "filter.uniclaude.required", "true"]);

  removePackage(manifest, UNICLAUDE_NAME);
  writeManifest(manifestPath, manifest);

  return { result: "ok", mode: "ninja" };
}
