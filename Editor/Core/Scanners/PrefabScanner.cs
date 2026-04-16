using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UniClaude.Editor
{
    /// <summary>
    /// Scans Unity prefab files (.prefab) by parsing YAML to extract
    /// GameObject names and script references. Shares parsing logic with
    /// <see cref="SceneScanner"/> via <see cref="UnityYamlParser"/>.
    /// </summary>
    public class PrefabScanner : IAssetScanner
    {
        /// <inheritdoc />
        public AssetKind Kind => AssetKind.Prefab;

        /// <inheritdoc />
        public bool CanScan(string assetPath)
        {
            return assetPath != null && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public IndexEntry Scan(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;

            var content = File.ReadAllText(assetPath);
            if (string.IsNullOrWhiteSpace(content)) return null;

            var gameObjects = UnityYamlParser.ExtractGameObjectNames(content);
            var scriptGuids = UnityYamlParser.ExtractScriptGuids(content);

            if (gameObjects.Count == 0 && scriptGuids.Count == 0) return null;

            var prefabName = Path.GetFileNameWithoutExtension(assetPath);
            var symbols = new List<string> { prefabName };
            symbols.AddRange(gameObjects);

            var rootObject = gameObjects.Count > 0 ? gameObjects[0] : prefabName;
            var summary = $"{prefabName} (Prefab — root: {rootObject})\n  GameObjects: {string.Join(", ", gameObjects.Take(15))}";
            if (gameObjects.Count > 15)
                summary += $" (+{gameObjects.Count - 15} more)";
            if (scriptGuids.Count > 0)
                summary += $"\n  Scripts: {scriptGuids.Count} referenced";

            return new IndexEntry
            {
                AssetPath = assetPath,
                Kind = AssetKind.Prefab,
                Name = prefabName,
                Symbols = symbols.Distinct().ToArray(),
                Dependencies = scriptGuids.ToArray(),
                Summary = summary,
                LastModifiedTicks = new FileInfo(assetPath).LastWriteTimeUtc.Ticks
            };
        }
    }
}
