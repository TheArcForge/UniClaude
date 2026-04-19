"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.discoverPlugins = discoverPlugins;
// src/plugins.ts
const node_fs_1 = require("node:fs");
const node_path_1 = require("node:path");
const node_os_1 = require("node:os");
function isValidPlugin(dir) {
    return (0, node_fs_1.existsSync)((0, node_path_1.join)(dir, ".claude-plugin", "plugin.json"));
}
/**
 * Read ~/.claude/plugins/installed_plugins.json and return install paths
 * for all plugins that have a valid .claude-plugin/plugin.json.
 */
function scanInstalledPlugins(homeDir) {
    const manifestPath = (0, node_path_1.join)(homeDir, ".claude", "plugins", "installed_plugins.json");
    if (!(0, node_fs_1.existsSync)(manifestPath))
        return [];
    let manifest;
    try {
        manifest = JSON.parse((0, node_fs_1.readFileSync)(manifestPath, "utf-8"));
    }
    catch (err) {
        console.error(`[plugins] Cannot read installed_plugins.json:`, err);
        return [];
    }
    if (!manifest.plugins)
        return [];
    const entries = [];
    const seen = new Set();
    for (const installations of Object.values(manifest.plugins)) {
        for (const install of installations) {
            if (!install.installPath)
                continue;
            if (seen.has(install.installPath))
                continue;
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
function scanMarketplacePlugins(homeDir, exclude) {
    const marketplacesDir = (0, node_path_1.join)(homeDir, ".claude", "plugins", "marketplaces");
    if (!(0, node_fs_1.existsSync)(marketplacesDir))
        return [];
    const entries = [];
    let marketplaces;
    try {
        marketplaces = (0, node_fs_1.readdirSync)(marketplacesDir, { withFileTypes: true });
    }
    catch (err) {
        console.error(`[plugins] Cannot read marketplaces directory ${marketplacesDir}:`, err);
        return [];
    }
    for (const marketplace of marketplaces) {
        if (!marketplace.isDirectory())
            continue;
        const pluginsDir = (0, node_path_1.join)(marketplacesDir, marketplace.name, "plugins");
        if (!(0, node_fs_1.existsSync)(pluginsDir))
            continue;
        let plugins;
        try {
            plugins = (0, node_fs_1.readdirSync)(pluginsDir, { withFileTypes: true });
        }
        catch (err) {
            console.error(`[plugins] Cannot read plugins directory ${pluginsDir}:`, err);
            continue;
        }
        for (const plugin of plugins) {
            if (!plugin.isDirectory())
                continue;
            const pluginPath = (0, node_path_1.join)(pluginsDir, plugin.name);
            if (exclude.has(pluginPath))
                continue;
            if (isValidPlugin(pluginPath)) {
                entries.push({ type: "local", path: pluginPath });
            }
        }
    }
    return entries;
}
function scanProjectPlugins(projectDir) {
    const pluginsDir = (0, node_path_1.join)(projectDir, ".claude", "plugins");
    if (!(0, node_fs_1.existsSync)(pluginsDir))
        return [];
    const entries = [];
    let plugins;
    try {
        plugins = (0, node_fs_1.readdirSync)(pluginsDir, { withFileTypes: true });
    }
    catch (err) {
        console.error(`[plugins] Cannot read project plugins directory ${pluginsDir}:`, err);
        return [];
    }
    for (const plugin of plugins) {
        if (!plugin.isDirectory())
            continue;
        const pluginPath = (0, node_path_1.join)(pluginsDir, plugin.name);
        if (isValidPlugin(pluginPath)) {
            entries.push({ type: "local", path: pluginPath });
        }
    }
    return entries;
}
function discoverPlugins(projectDir, homeDir) {
    const home = homeDir ?? (0, node_os_1.homedir)();
    const entries = [];
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
//# sourceMappingURL=plugins.js.map