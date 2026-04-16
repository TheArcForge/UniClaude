using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniClaude.Editor
{
    /// <summary>
    /// Scans .shader files to extract shader name and exposed properties.
    /// Skips ShaderGraph (.shadergraph) files which are binary.
    /// </summary>
    public class ShaderScanner : IAssetScanner
    {
        static readonly Regex ShaderNameRegex = new(@"Shader\s+""([^""]+)""");
        static readonly Regex PropertyRegex = new(@"(\w+)\s*\(""([^""]*)"",\s*(\w+)\)");

        /// <inheritdoc />
        public AssetKind Kind => AssetKind.Shader;

        /// <inheritdoc />
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>True if the path ends with ".shader" (case-insensitive).</returns>
        public bool CanScan(string assetPath)
        {
            return assetPath != null && assetPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        /// <param name="assetPath">Absolute or relative path to the .shader file.</param>
        /// <returns>
        /// An <see cref="IndexEntry"/> with the shader name, property symbols, and a compact summary;
        /// or <c>null</c> if the file is missing, empty, or contains no Shader declaration.
        /// </returns>
        public IndexEntry Scan(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;

            var content = File.ReadAllText(assetPath);
            if (string.IsNullOrWhiteSpace(content)) return null;

            var nameMatch = ShaderNameRegex.Match(content);
            if (!nameMatch.Success) return null;

            var shaderName = nameMatch.Groups[1].Value;
            var displayName = Path.GetFileNameWithoutExtension(assetPath);
            var symbols = new List<string> { displayName, shaderName };

            var properties = new List<string>();
            foreach (Match m in PropertyRegex.Matches(content))
            {
                var propName = m.Groups[1].Value;
                var propLabel = m.Groups[2].Value;
                var propType = m.Groups[3].Value;
                symbols.Add(propName);
                properties.Add($"{propName} (\"{propLabel}\", {propType})");
            }

            var summary = $"{shaderName} (Shader)";
            if (properties.Count > 0)
                summary += "\n  Properties: " + string.Join(", ", properties.Take(10));

            return new IndexEntry
            {
                AssetPath = assetPath,
                Kind = AssetKind.Shader,
                Name = displayName,
                Symbols = symbols.Distinct().ToArray(),
                Dependencies = Array.Empty<string>(),
                Summary = summary,
                LastModifiedTicks = new FileInfo(assetPath).LastWriteTimeUtc.Ticks
            };
        }
    }
}
