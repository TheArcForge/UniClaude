import { readFileSync, writeFileSync } from "node:fs";

export function readManifest(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

export function writeManifest(path, manifest) {
  writeFileSync(path, JSON.stringify(manifest, null, 2) + "\n", "utf8");
}

export function removePackage(manifest, name) {
  if (manifest.dependencies) delete manifest.dependencies[name];
}

export function addPackage(manifest, name, spec) {
  if (!manifest.dependencies) manifest.dependencies = {};
  manifest.dependencies[name] = spec;
}
