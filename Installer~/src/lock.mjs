import { readFileSync, writeFileSync } from "node:fs";

export function readLock(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

export function writeLock(path, lock) {
  writeFileSync(path, JSON.stringify(lock, null, 2) + "\n", "utf8");
}

export function stripEntries(lock, names) {
  if (!lock.dependencies) return;
  for (const name of names) delete lock.dependencies[name];
}

export function insertEntries(lock, entriesByName) {
  if (!lock.dependencies) lock.dependencies = {};
  const merged = { ...lock.dependencies, ...entriesByName };
  const sorted = Object.keys(merged).sort();
  lock.dependencies = {};
  for (const k of sorted) lock.dependencies[k] = merged[k];
}

export function hasEntry(lock, name) {
  return !!(lock.dependencies && lock.dependencies[name]);
}
