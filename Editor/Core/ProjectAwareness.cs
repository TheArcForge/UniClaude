using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UniClaude.Editor
{
    /// <summary>
    /// Coordinates the project awareness pipeline: scanners, index store, retriever, and formatter.
    /// Provides the <see cref="GetContext"/> method for injecting project context into CLI messages.
    /// </summary>
    public class ProjectAwareness : IDisposable
    {
        /// <summary>
        /// Static reference for <see cref="UniClaudeAssetPostprocessor"/> discovery.
        /// Set during <see cref="Initialize"/>, cleared during <see cref="Dispose"/>.
        /// </summary>
        public static ProjectAwareness Instance { get; private set; }

        ProjectIndex _index;
        ScannerRegistry _scannerRegistry;
        IIndexRetriever _retriever;
        string _projectRoot;
        UniClaudeSettings _settings;
        List<PackageInfo> _packages;

        /// <summary>
        /// Initializes the project awareness system. Loads cached index from disk,
        /// discovers packages, and performs a lightweight scan for new/changed files.
        /// </summary>
        /// <param name="projectRoot">The root directory of the Unity project.</param>
        public void Initialize(string projectRoot)
        {
            _projectRoot = projectRoot;
            Instance = this;

            _scannerRegistry = new ScannerRegistry();
            _scannerRegistry.Register(new ScriptScanner());
            _scannerRegistry.Register(new SceneScanner());
            _scannerRegistry.Register(new PrefabScanner());
            _scannerRegistry.Register(new ScriptableObjectScanner());
            _scannerRegistry.Register(new ProjectSettingsScanner());
            _scannerRegistry.Register(new ShaderScanner());

            _retriever = new KeywordRetriever();
            _settings = UniClaudeSettings.Load();

            // Load cached index or create empty
            _index = ProjectIndexStore.Load() ?? new ProjectIndex();

            // Set project metadata
            _index.ProjectName = Path.GetFileName(projectRoot);
            _index.UnityVersion = Application.unityVersion;

            // Discover packages
            _packages = PackageDiscovery.Resolve(projectRoot);
            _index.IndexedPackages = _packages
                .Where(p => IndexFilterSettings.IsPackageIncluded(p, _settings))
                .ToList();

            // Lightweight scan
            LightweightScan();

            // Save updated index
            ProjectIndexStore.Save(_index);
        }

        /// <summary>
        /// Returns the current project index.
        /// </summary>
        /// <returns>The project index.</returns>
        public ProjectIndex GetIndex()
        {
            return _index;
        }

        /// <summary>
        /// Returns the discovered packages list (both included and excluded).
        /// Used by the Settings UI to display package toggles.
        /// </summary>
        /// <returns>All discovered packages.</returns>
        public List<PackageInfo> GetDiscoveredPackages()
        {
            return _packages ?? new List<PackageInfo>();
        }

        /// <summary>
        /// Returns the Tier 1 project summary for system prompt injection.
        /// Lightweight — no query, no retrieval, just project metadata and tree.
        /// </summary>
        /// <returns>The formatted Tier 1 context string.</returns>
        public string GetTier1Context()
        {
            return ContextFormatter.FormatTier1(_index);
        }

        /// <summary>
        /// Retrieves project context relevant to the given user query.
        /// </summary>
        /// <param name="query">The user's message text.</param>
        /// <param name="settings">Optional retrieval settings. Uses defaults if null.</param>
        /// <returns>A <see cref="ContextResult"/> with formatted prompt and display data.</returns>
        public ContextResult GetContext(string query, RetrievalSettings settings = null)
        {
            settings = settings ?? new RetrievalSettings();

            var result = _retriever.Retrieve(query, _index, settings);
            return ContextFormatter.Format(_index, result);
        }

        /// <summary>
        /// Performs a full-depth rebuild of the index, scanning all assets including
        /// full YAML parsing for scenes and prefabs, plus included packages.
        /// </summary>
        /// <returns>A summary string describing what was indexed.</returns>
        public string FullRebuild()
        {
            _settings = UniClaudeSettings.Load();
            _packages = PackageDiscovery.Resolve(_projectRoot);
            _index.Entries.Clear();

            Func<string, bool> folderFilter = path =>
                !IndexFilterSettings.IsPathExcluded(path, _settings);

            ScanDirectory(Path.Combine(_projectRoot, "Assets"), null, folderFilter);
            ScanDirectory(Path.Combine(_projectRoot, "ProjectSettings"), null, null);

            // Scan included packages
            var includedPackages = _packages
                .Where(p => IndexFilterSettings.IsPackageIncluded(p, _settings))
                .ToList();
            _index.IndexedPackages = includedPackages;

            foreach (var pkg in includedPackages)
            {
                if (pkg.ResolvedPath != null && Directory.Exists(pkg.ResolvedPath))
                    ScanPackage(pkg);
            }

            _index.LastFullScan = DateTime.UtcNow.ToString("o");
            RegenerateTreeSummary();
            ProjectIndexStore.Save(_index);

            var scripts = _index.Entries.Count(e => e.Kind == AssetKind.Script);
            var scenes = _index.Entries.Count(e => e.Kind == AssetKind.Scene);
            var prefabs = _index.Entries.Count(e => e.Kind == AssetKind.Prefab);
            return $"Index rebuilt: {scripts} scripts, {scenes} scenes, {prefabs} prefabs, " +
                   $"{_index.Entries.Count} total entries";
        }

        /// <summary>
        /// Handles asset changes from the AssetPostprocessor.
        /// Re-scans imported files, removes deleted entries, updates moved paths.
        /// </summary>
        /// <param name="imported">Paths of imported or changed assets.</param>
        /// <param name="deleted">Paths of deleted assets.</param>
        /// <param name="moved">New paths of moved assets.</param>
        /// <param name="movedFrom">Original paths of moved assets.</param>
        public void HandleAssetsChanged(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = false;

            // Re-scan imported/changed
            foreach (var path in imported)
            {
                var rel = MakeRelative(path);

                // Skip files in excluded folders
                if (IndexFilterSettings.IsPathExcluded(rel, _settings))
                    continue;

                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_projectRoot, path);
                var entry = _scannerRegistry.Scan(fullPath);
                if (entry != null)
                {
                    _index.Entries.RemoveAll(e => e.AssetPath == rel);
                    entry.AssetPath = rel;

                    // Detect source from path
                    if (rel.StartsWith("Packages/"))
                    {
                        var parts = rel.Split('/');
                        if (parts.Length >= 2)
                            entry.Source = parts[1];
                    }

                    _index.Entries.Add(entry);
                    changed = true;
                }
            }

            // Remove deleted
            foreach (var path in deleted)
            {
                var rel = MakeRelative(path);
                if (_index.Entries.RemoveAll(e => e.AssetPath == rel) > 0)
                    changed = true;
            }

            // Update moved
            for (int i = 0; i < moved.Length && i < movedFrom.Length; i++)
            {
                var relFrom = MakeRelative(movedFrom[i]);
                var existing = _index.Entries.Find(e => e.AssetPath == relFrom);
                if (existing != null)
                {
                    existing.AssetPath = MakeRelative(moved[i]);
                    existing.Name = Path.GetFileNameWithoutExtension(moved[i]);
                    changed = true;
                }
            }

            if (changed)
            {
                RegenerateTreeSummary();
                ProjectIndexStore.Save(_index);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Instance == this)
                Instance = null;
        }

        void LightweightScan()
        {
            var existingPaths = new HashSet<string>(_index.Entries.Select(e => e.AssetPath));
            var foundPaths = new HashSet<string>();

            Func<string, bool> folderFilter = path =>
                !IndexFilterSettings.IsPathExcluded(path, _settings);

            // Scan Assets directory
            ScanDirectoryLightweight(Path.Combine(_projectRoot, "Assets"), existingPaths, foundPaths, null, folderFilter);

            // Scan ProjectSettings
            ScanDirectoryLightweight(Path.Combine(_projectRoot, "ProjectSettings"), existingPaths, foundPaths, null, null);

            // Scan included packages
            var includedPackages = _packages
                .Where(p => IndexFilterSettings.IsPackageIncluded(p, _settings))
                .ToList();
            _index.IndexedPackages = includedPackages;

            foreach (var pkg in includedPackages)
            {
                if (pkg.ResolvedPath != null && Directory.Exists(pkg.ResolvedPath))
                    ScanPackageLightweight(pkg, existingPaths, foundPaths);
            }

            // Remove entries for files not found in any scanned directory
            _index.Entries.RemoveAll(e => !foundPaths.Contains(e.AssetPath));

            RegenerateTreeSummary();
        }

        void ScanPackage(PackageInfo pkg)
        {
            foreach (var dir in Directory.GetDirectories(pkg.ResolvedPath))
            {
                var dirName = Path.GetFileName(dir);
                if (IndexFilterSettings.IsPackageFolderSkipped(dirName))
                    continue;

                foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    var entry = _scannerRegistry.Scan(file);
                    if (entry != null)
                    {
                        entry.AssetPath = MakePackageRelative(file, pkg);
                        entry.Source = pkg.Name;
                        _index.Entries.Add(entry);
                    }
                }
            }

            // Also scan files directly in package root (not in subdirs)
            foreach (var file in Directory.GetFiles(pkg.ResolvedPath))
            {
                var entry = _scannerRegistry.Scan(file);
                if (entry != null)
                {
                    entry.AssetPath = MakePackageRelative(file, pkg);
                    entry.Source = pkg.Name;
                    _index.Entries.Add(entry);
                }
            }
        }

        void ScanPackageLightweight(PackageInfo pkg, HashSet<string> existingPaths, HashSet<string> foundPaths)
        {
            var filesToScan = new List<string>();

            foreach (var dir in Directory.GetDirectories(pkg.ResolvedPath))
            {
                var dirName = Path.GetFileName(dir);
                if (IndexFilterSettings.IsPackageFolderSkipped(dirName))
                    continue;

                filesToScan.AddRange(Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories));
            }

            // Also files directly in package root
            filesToScan.AddRange(Directory.GetFiles(pkg.ResolvedPath));

            foreach (var file in filesToScan)
            {
                var relativePath = MakePackageRelative(file, pkg);
                foundPaths.Add(relativePath);

                var existing = _index.Entries.Find(e => e.AssetPath == relativePath);
                if (existing != null)
                {
                    var currentTicks = new FileInfo(file).LastWriteTimeUtc.Ticks;
                    if (existing.LastModifiedTicks == currentTicks)
                        continue;

                    _index.Entries.Remove(existing);
                }

                var entry = _scannerRegistry.Scan(file);
                if (entry != null)
                {
                    entry.AssetPath = relativePath;
                    entry.Source = pkg.Name;
                    _index.Entries.Add(entry);
                }
            }
        }

        void ScanDirectoryLightweight(string dir, HashSet<string> existingPaths, HashSet<string> foundPaths,
            string source, Func<string, bool> pathFilter)
        {
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = MakeRelative(file);

                if (pathFilter != null && !pathFilter(relativePath))
                    continue;

                foundPaths.Add(relativePath);

                // Check if already indexed and not stale
                var existing = _index.Entries.Find(e => e.AssetPath == relativePath);
                if (existing != null)
                {
                    var currentTicks = new FileInfo(file).LastWriteTimeUtc.Ticks;
                    if (existing.LastModifiedTicks == currentTicks)
                        continue; // Up to date

                    // Stale — re-scan
                    _index.Entries.Remove(existing);
                }

                var entry = _scannerRegistry.Scan(file);
                if (entry != null)
                {
                    entry.AssetPath = relativePath;
                    entry.Source = source;
                    _index.Entries.Add(entry);
                }
            }
        }

        void ScanDirectory(string dir, string source, Func<string, bool> pathFilter)
        {
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = MakeRelative(file);

                if (pathFilter != null && !pathFilter(relativePath))
                    continue;

                var entry = _scannerRegistry.Scan(file);
                if (entry != null)
                {
                    entry.AssetPath = relativePath;
                    entry.Source = source;
                    _index.Entries.Add(entry);
                }
            }
        }

        string MakeRelative(string absolutePath)
        {
            if (_projectRoot != null && absolutePath.StartsWith(_projectRoot))
            {
                var relative = absolutePath.Substring(_projectRoot.Length);
                if (relative.Length > 0 && (relative[0] == '/' || relative[0] == '\\'))
                    relative = relative.Substring(1);
                return relative;
            }
            return absolutePath;
        }

        string MakePackageRelative(string absolutePath, PackageInfo pkg)
        {
            if (pkg.ResolvedPath != null && absolutePath.StartsWith(pkg.ResolvedPath))
            {
                var suffix = absolutePath.Substring(pkg.ResolvedPath.Length);
                if (suffix.Length > 0 && (suffix[0] == '/' || suffix[0] == '\\'))
                    suffix = suffix.Substring(1);
                return $"Packages/{pkg.Name}/{suffix}";
            }
            return MakeRelative(absolutePath);
        }

        void RegenerateTreeSummary()
        {
            var treeEntries = _index.Entries
                .Where(e => e.Kind == AssetKind.Script || e.Kind == AssetKind.Scene
                         || e.Kind == AssetKind.Prefab || e.Kind == AssetKind.Shader)
                .ToList();

            if (treeEntries.Count == 0)
            {
                _index.ProjectTreeSummary = "";
                return;
            }

            var budget = _settings?.ContextTokenBudget ?? 3300;
            var unlimited = budget <= 0;

            // Group files by directory
            var dirGroups = treeEntries
                .GroupBy(e => Path.GetDirectoryName(e.AssetPath)?.Replace("\\", "/") ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());

            var expanded = new HashSet<string>();
            var sb = new System.Text.StringBuilder();
            int charCount = 0;

            // Collect all directories sorted by depth then name
            var allDirs = dirGroups.Keys
                .Where(d => !string.IsNullOrEmpty(d))
                .OrderBy(d => d.Count(c => c == '/'))
                .ThenBy(d => d)
                .ToList();

            // BFS queue — seed with shallowest directories
            var queue = new Queue<string>();
            var enqueued = new HashSet<string>();

            foreach (var dir in allDirs)
            {
                // Enqueue if no parent directory exists in the groups
                var parent = Path.GetDirectoryName(dir)?.Replace("\\", "/") ?? "";
                if (string.IsNullOrEmpty(parent) || !dirGroups.ContainsKey(parent))
                {
                    if (enqueued.Add(dir))
                        queue.Enqueue(dir);
                }
            }

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                if (expanded.Contains(dir)) continue;
                expanded.Add(dir);

                if (!dirGroups.TryGetValue(dir, out var files)) continue;

                // Check if we're over budget
                if (!unlimited && charCount / 4 >= budget)
                {
                    var summary = BuildFolderSummary(dir, files);
                    sb.AppendLine(summary);
                    charCount += summary.Length + 1;
                    continue;
                }

                // Expand: list directory header and files
                var header = dir + "/";
                sb.AppendLine(header);
                charCount += header.Length + 1;

                foreach (var file in files.OrderBy(f => f.AssetPath))
                {
                    var line = "  " + Path.GetFileName(file.AssetPath);
                    sb.AppendLine(line);
                    charCount += line.Length + 1;
                }

                // Enqueue direct child directories
                foreach (var childDir in allDirs.Where(d =>
                    d.StartsWith(dir + "/") &&
                    d.Substring(dir.Length + 1).IndexOf('/') < 0 &&
                    !expanded.Contains(d)))
                {
                    if (enqueued.Add(childDir))
                        queue.Enqueue(childDir);
                }
            }

            // Summarize any remaining unexpanded directories
            foreach (var dir in allDirs.Where(d => !expanded.Contains(d)))
            {
                if (dirGroups.TryGetValue(dir, out var files))
                {
                    var summary = BuildFolderSummary(dir, files);
                    sb.AppendLine(summary);
                }
            }

            _index.ProjectTreeSummary = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Builds a one-line summary for a folder, e.g. "Assets/Scripts/AI/ — 23 files (14 .cs, 9 .prefab)"
        /// </summary>
        static string BuildFolderSummary(string dir, List<IndexEntry> files)
        {
            var byExt = files
                .GroupBy(f => Path.GetExtension(f.AssetPath).ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Count()} {g.Key}")
                .ToArray();

            return $"{dir}/ \u2014 {files.Count} file{(files.Count != 1 ? "s" : "")} ({string.Join(", ", byExt)})";
        }
    }
}
