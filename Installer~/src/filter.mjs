import { stripEntries, insertEntries } from "./lock.mjs";

function parse(s) { return JSON.parse(s); }
function serialize(obj) { return JSON.stringify(obj, null, 2) + "\n"; }

export function cleanLockJson(jsonText, owned) {
  const lock = parse(jsonText);
  stripEntries(lock, Object.keys(owned));
  return serialize(lock);
}

export function smudgeLockJson(jsonText, owned) {
  const lock = parse(jsonText);
  insertEntries(lock, owned);
  return serialize(lock);
}
