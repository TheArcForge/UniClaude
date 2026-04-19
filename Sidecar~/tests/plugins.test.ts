// tests/plugins.test.ts
import { describe, it, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, writeFileSync, rmSync, mkdtempSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { discoverPlugins } from "../src/plugins.js";

describe("discoverPlugins", () => {
  let tempDir: string;

  beforeEach(() => {
    tempDir = mkdtempSync(join(tmpdir(), "uniclaude-plugins-test-"));
  });

  afterEach(() => {
    rmSync(tempDir, { recursive: true, force: true });
  });

  function createPlugin(basePath: string): void {
    mkdirSync(join(basePath, ".claude-plugin"), { recursive: true });
    writeFileSync(
      join(basePath, ".claude-plugin", "plugin.json"),
      JSON.stringify({ name: "test-plugin" })
    );
  }

  function writeInstalledPlugins(
    homeDir: string,
    plugins: Record<string, Array<{ installPath: string }>>
  ): void {
    const dir = join(homeDir, ".claude", "plugins");
    mkdirSync(dir, { recursive: true });
    writeFileSync(
      join(dir, "installed_plugins.json"),
      JSON.stringify({ version: 2, plugins })
    );
  }

  it("returns empty array when no plugin directories exist", () => {
    const result = discoverPlugins(undefined, tempDir);
    assert.deepEqual(result, []);
  });

  it("discovers plugins from installed_plugins.json", () => {
    const pluginDir = join(tempDir, ".claude", "plugins", "cache", "mp", "my-plugin", "1.0.0");
    createPlugin(pluginDir);
    writeInstalledPlugins(tempDir, {
      "my-plugin@mp": [{ installPath: pluginDir }],
    });

    const result = discoverPlugins(undefined, tempDir);
    assert.equal(result.length, 1);
    assert.equal(result[0].type, "local");
    assert.equal(result[0].path, pluginDir);
  });

  it("skips installed plugins without valid .claude-plugin/plugin.json", () => {
    const pluginDir = join(tempDir, ".claude", "plugins", "cache", "mp", "bad-plugin", "1.0.0");
    mkdirSync(pluginDir, { recursive: true });
    writeInstalledPlugins(tempDir, {
      "bad-plugin@mp": [{ installPath: pluginDir }],
    });

    const result = discoverPlugins(undefined, tempDir);
    assert.deepEqual(result, []);
  });

  it("discovers marketplace plugins not in installed_plugins.json", () => {
    const pluginDir = join(
      tempDir, ".claude", "plugins", "marketplaces",
      "my-marketplace", "plugins", "my-plugin"
    );
    createPlugin(pluginDir);

    const result = discoverPlugins(undefined, tempDir);
    assert.equal(result.length, 1);
    assert.equal(result[0].type, "local");
    assert.equal(result[0].path, pluginDir);
  });

  it("does not duplicate plugins found in both installed and marketplaces", () => {
    const marketplaceDir = join(
      tempDir, ".claude", "plugins", "marketplaces",
      "mp", "plugins", "my-plugin"
    );
    createPlugin(marketplaceDir);
    writeInstalledPlugins(tempDir, {
      "my-plugin@mp": [{ installPath: marketplaceDir }],
    });

    const result = discoverPlugins(undefined, tempDir);
    assert.equal(result.length, 1);
    assert.equal(result[0].path, marketplaceDir);
  });

  it("discovers project-level plugins", () => {
    const projectDir = join(tempDir, "my-project");
    const pluginDir = join(projectDir, ".claude", "plugins", "local-plugin");
    createPlugin(pluginDir);

    const result = discoverPlugins(projectDir, tempDir);
    assert.equal(result.length, 1);
    assert.equal(result[0].type, "local");
    assert.equal(result[0].path, pluginDir);
  });

  it("skips marketplace directories without .claude-plugin/plugin.json", () => {
    const pluginDir = join(
      tempDir, ".claude", "plugins", "marketplaces",
      "mp", "plugins", "bad-plugin"
    );
    mkdirSync(pluginDir, { recursive: true });

    const result = discoverPlugins(undefined, tempDir);
    assert.deepEqual(result, []);
  });

  it("returns installed, marketplace, and project plugins together", () => {
    // Installed plugin (in cache)
    const cachedPlugin = join(tempDir, ".claude", "plugins", "cache", "mp", "cached-plugin", "1.0.0");
    createPlugin(cachedPlugin);
    writeInstalledPlugins(tempDir, {
      "cached-plugin@mp": [{ installPath: cachedPlugin }],
    });

    // Marketplace plugin (not in installed_plugins.json)
    const marketplacePlugin = join(
      tempDir, ".claude", "plugins", "marketplaces",
      "mp2", "plugins", "mp-plugin"
    );
    createPlugin(marketplacePlugin);

    // Project plugin
    const projectDir = join(tempDir, "my-project");
    const projectPlugin = join(projectDir, ".claude", "plugins", "proj-plugin");
    createPlugin(projectPlugin);

    const result = discoverPlugins(projectDir, tempDir);
    assert.equal(result.length, 3);

    const paths = result.map((r) => r.path);
    assert.ok(paths.includes(cachedPlugin));
    assert.ok(paths.includes(marketplacePlugin));
    assert.ok(paths.includes(projectPlugin));
  });

  it("returns only user-level plugins when projectDir is undefined", () => {
    const cachedPlugin = join(tempDir, ".claude", "plugins", "cache", "mp", "my-plugin", "1.0.0");
    createPlugin(cachedPlugin);
    writeInstalledPlugins(tempDir, {
      "my-plugin@mp": [{ installPath: cachedPlugin }],
    });

    const result = discoverPlugins(undefined, tempDir);
    assert.equal(result.length, 1);
    assert.equal(result[0].path, cachedPlugin);
  });

  it("deduplicates multiple installations of the same plugin", () => {
    const pluginDir = join(tempDir, ".claude", "plugins", "cache", "mp", "my-plugin", "1.0.0");
    createPlugin(pluginDir);
    writeInstalledPlugins(tempDir, {
      "my-plugin@mp": [
        { installPath: pluginDir },
        { installPath: pluginDir },
      ],
    });

    const result = discoverPlugins(undefined, tempDir);
    assert.equal(result.length, 1);
  });

  it("handles malformed installed_plugins.json gracefully", () => {
    const dir = join(tempDir, ".claude", "plugins");
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, "installed_plugins.json"), "not valid json");

    const result = discoverPlugins(undefined, tempDir);
    assert.deepEqual(result, []);
  });
});
