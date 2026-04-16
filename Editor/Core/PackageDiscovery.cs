using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UniClaude.Editor
{
    /// <summary>
    /// Discovers installed Unity packages by parsing <c>packages-lock.json</c>.
    /// Falls back to scanning the <c>Packages/</c> directory for local packages
    /// if the lock file is missing or corrupt.
    /// </summary>
    public static class PackageDiscovery
    {
        /// <summary>
        /// Resolves all installed packages for the given project root.
        /// </summary>
        /// <param name="projectRoot">Absolute path to the Unity project root.</param>
        /// <returns>List of discovered packages with metadata and resolved paths.</returns>
        public static List<PackageInfo> Resolve(string projectRoot)
        {
            var packagesDir = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(packagesDir))
                return new List<PackageInfo>();

            var lockPath = Path.Combine(packagesDir, "packages-lock.json");
            if (File.Exists(lockPath))
            {
                try
                {
                    return ParseLockFile(lockPath, projectRoot);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UniClaude] Failed to parse packages-lock.json: {ex.Message}. Falling back to local scan.");
                }
            }
            else
            {
                Debug.LogWarning("[UniClaude] packages-lock.json not found. Falling back to local package scan.");
            }

            return ScanLocalPackages(packagesDir);
        }

        static List<PackageInfo> ParseLockFile(string lockPath, string projectRoot)
        {
            var json = File.ReadAllText(lockPath);
            var root = JObject.Parse(json);
            var deps = root["dependencies"] as JObject;
            if (deps == null)
                return new List<PackageInfo>();

            var results = new List<PackageInfo>();
            var packagesDir = Path.Combine(projectRoot, "Packages");

            foreach (var prop in deps.Properties())
            {
                var name = prop.Name;
                var entry = prop.Value as JObject;
                if (entry == null) continue;

                var version = entry["version"]?.ToString() ?? "";
                var source = entry["source"]?.ToString() ?? "";
                var isLocal = source == "embedded" || source == "local";

                var info = new PackageInfo
                {
                    Name = name,
                    Version = version,
                    IsLocal = isLocal
                };

                if (isLocal)
                {
                    info.ResolvedPath = ResolveLocalPath(packagesDir, name);
                    info.DisplayName = ReadDisplayName(info.ResolvedPath) ?? name;
                }
                else
                {
                    info.ResolvedPath = ResolveCachePath(projectRoot, name, version);
                    info.DisplayName = name;
                }

                results.Add(info);
            }

            return results;
        }

        static string ResolveLocalPath(string packagesDir, string packageName)
        {
            var direct = Path.Combine(packagesDir, packageName);
            if (Directory.Exists(direct))
                return direct;

            foreach (var dir in Directory.GetDirectories(packagesDir))
            {
                var pkgJson = Path.Combine(dir, "package.json");
                if (!File.Exists(pkgJson)) continue;

                try
                {
                    var json = File.ReadAllText(pkgJson);
                    var obj = JObject.Parse(json);
                    if (obj["name"]?.ToString() == packageName)
                        return dir;
                }
                catch { }
            }

            return null;
        }

        static string ResolveCachePath(string projectRoot, string name, string version)
        {
            var cacheDir = Path.Combine(projectRoot, "Library", "PackageCache");
            if (!Directory.Exists(cacheDir))
                return null;

            var prefix = $"{name}@{version}";
            foreach (var dir in Directory.GetDirectories(cacheDir))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == prefix || dirName.StartsWith(prefix + "-"))
                    return dir;
            }

            return null;
        }

        static List<PackageInfo> ScanLocalPackages(string packagesDir)
        {
            var results = new List<PackageInfo>();

            foreach (var dir in Directory.GetDirectories(packagesDir))
            {
                var pkgJson = Path.Combine(dir, "package.json");
                if (!File.Exists(pkgJson)) continue;

                try
                {
                    var json = File.ReadAllText(pkgJson);
                    var obj = JObject.Parse(json);
                    results.Add(new PackageInfo
                    {
                        Name = obj["name"]?.ToString() ?? Path.GetFileName(dir),
                        Version = obj["version"]?.ToString() ?? "",
                        DisplayName = obj["displayName"]?.ToString() ?? Path.GetFileName(dir),
                        IsLocal = true,
                        ResolvedPath = dir
                    });
                }
                catch { }
            }

            return results;
        }

        static string ReadDisplayName(string packageDir)
        {
            if (packageDir == null) return null;

            var pkgJson = Path.Combine(packageDir, "package.json");
            if (!File.Exists(pkgJson)) return null;

            try
            {
                var json = File.ReadAllText(pkgJson);
                var obj = JObject.Parse(json);
                return obj["displayName"]?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
