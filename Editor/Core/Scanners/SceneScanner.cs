using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UniClaude.Editor
{
    /// <summary>
    /// Scans Unity scene files (.unity) by parsing YAML to extract
    /// GameObject names and script references.
    /// </summary>
    public class SceneScanner : IAssetScanner
    {
        /// <inheritdoc />
        public AssetKind Kind => AssetKind.Scene;

        /// <inheritdoc />
        public bool CanScan(string assetPath)
        {
            return assetPath != null && assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
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

            var sceneName = Path.GetFileNameWithoutExtension(assetPath);
            var symbols = new List<string> { sceneName };
            symbols.AddRange(gameObjects);

            var summary = $"{sceneName} (Scene)\n  GameObjects: {string.Join(", ", gameObjects.Take(20))}";
            if (gameObjects.Count > 20)
                summary += $" (+{gameObjects.Count - 20} more)";
            if (scriptGuids.Count > 0)
                summary += $"\n  Scripts: {scriptGuids.Count} referenced";

            return new IndexEntry
            {
                AssetPath = assetPath,
                Kind = AssetKind.Scene,
                Name = sceneName,
                Symbols = symbols.Distinct().ToArray(),
                Dependencies = scriptGuids.ToArray(),
                Summary = summary,
                LastModifiedTicks = new FileInfo(assetPath).LastWriteTimeUtc.Ticks
            };
        }
    }
}
