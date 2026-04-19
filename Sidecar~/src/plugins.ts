// src/plugins.ts
import { existsSync, readFileSync, readdirSync, Dirent } from "node:fs";
import { join } from "node:path";
import { homedir } from "node:os";

export interface PluginEntry {
  type: "local";
  path: string;
}

function isValidPlugin(dir: string): boolean {
  return existsSync(join(dir, ".claude-plugin", "plugin.json"));
}

/**
 * Read ~/.claude/plugins/installed_plugins.json and return install paths
 * for all plugins that have a valid .claude-plugin/plugin.json.
 */
function scanInstalledPlugins(homeDir: string): PluginEntry[] {
  const manifestPath = join(homeDir, ".claude", "plugins", "installed_plugins.json");
  if (!existsSync(manifestPath)) return [];

  let manifest: { plugins?: Record<string, Array<{ installPath?: string }>> };
  try {
    manifest = JSON.parse(readFileSync(manifestPath, "utf-8"));
  } catch (err) {
    console.error(`[plugins] Cannot read installed_plugins.json:`, err);
    return [];
  }

  if (!manifest.plugins) return [];

  const entries: PluginEntry[] = [];
  const seen = new Set<string>();

  for (const installations of Object.values(manifest.plugins)) {
    for (const install of installations) {
      if (!install.installPath) continue;
      if (seen.has(install.installPath)) continue;
      seen.add(install.installPath);

      if (isValidPlugin(install.installPath)) {
        entries.push({ type: "local", path: install.installPath });
      }
    }
  }

  return entries;
}

/**
 * Scan ~/.claude/plugins/marketplaces/ for plugins not listed in
 * installed_plugins.json (fallback for marketplace-only plugins).
 */
function scanMarketplacePlugins(homeDir: string, exclude: Set<string>): PluginEntry[] {
  const marketplacesDir = join(homeDir, ".claude", "plugins", "marketplaces");
  if (!existsSync(marketplacesDir)) return [];

  const entries: PluginEntry[] = [];

  let marketplaces: Dirent<string>[];
  try {
    marketplaces = readdirSync(marketplacesDir, { withFileTypes: true });
  } catch (err) {
    console.error(`[plugins] Cannot read marketplaces directory ${marketplacesDir}:`, err);
    return [];
  }

  for (const marketplace of marketplaces) {
    if (!marketplace.isDirectory()) continue;

    const pluginsDir = join(marketplacesDir, marketplace.name, "plugins");
    if (!existsSync(pluginsDir)) continue;

    let plugins: Dirent<string>[];
    try {
      plugins = readdirSync(pluginsDir, { withFileTypes: true });
    } catch (err) {
      console.error(`[plugins] Cannot read plugins directory ${pluginsDir}:`, err);
      continue;
    }

    for (const plugin of plugins) {
      if (!plugin.isDirectory()) continue;

      const pluginPath = join(pluginsDir, plugin.name);
      if (exclude.has(pluginPath)) continue;

      if (isValidPlugin(pluginPath)) {
        entries.push({ type: "local", path: pluginPath });
      }
    }
  }

  return entries;
}

function scanProjectPlugins(projectDir: string): PluginEntry[] {
  const pluginsDir = join(projectDir, ".claude", "plugins");
  if (!existsSync(pluginsDir)) return [];

  const entries: PluginEntry[] = [];

  let plugins: Dirent<string>[];
  try {
    plugins = readdirSync(pluginsDir, { withFileTypes: true });
  } catch (err) {
    console.error(`[plugins] Cannot read project plugins directory ${pluginsDir}:`, err);
    return [];
  }

  for (const plugin of plugins) {
    if (!plugin.isDirectory()) continue;

    const pluginPath = join(pluginsDir, plugin.name);
    if (isValidPlugin(pluginPath)) {
      entries.push({ type: "local", path: pluginPath });
    }
  }

  return entries;
}

export function discoverPlugins(projectDir?: string, homeDir?: string): PluginEntry[] {
  const home = homeDir ?? homedir();
  const entries: PluginEntry[] = [];

  // Primary: read installed_plugins.json (covers cache/ directory)
  const installed = scanInstalledPlugins(home);
  entries.push(...installed);

  // Secondary: scan marketplaces/ for any not in installed_plugins.json
  const installedPaths = new Set(installed.map((e) => e.path));
  entries.push(...scanMarketplacePlugins(home, installedPaths));

  // Project-level plugins
  if (projectDir) {
    entries.push(...scanProjectPlugins(projectDir));
  }

  return entries;
}
