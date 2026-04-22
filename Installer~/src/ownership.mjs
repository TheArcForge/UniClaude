function reachable(lock, roots) {
  const visited = new Set();
  const stack = [...roots];
  while (stack.length) {
    const name = stack.pop();
    if (visited.has(name)) continue;
    visited.add(name);
    const entry = lock.dependencies?.[name];
    if (!entry) continue;
    for (const depName of Object.keys(entry.dependencies || {})) {
      if (!visited.has(depName)) stack.push(depName);
    }
  }
  return visited;
}

export function computeOwnership(manifest, lock, uniclaudeName) {
  const sharedRoots = Object.keys(manifest.dependencies || {})
    .filter(n => n !== uniclaudeName);
  const sharedReachable = reachable(lock, sharedRoots);
  const uniclaudeReachable = reachable(lock, [uniclaudeName]);

  const owned = {};
  for (const name of uniclaudeReachable) {
    if (sharedReachable.has(name)) continue;
    const entry = lock.dependencies?.[name];
    if (!entry) continue;
    owned[name] = entry;
  }
  return owned;
}
