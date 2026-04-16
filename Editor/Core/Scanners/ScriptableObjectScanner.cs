using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniClaude.Editor
{
    /// <summary>
    /// Scans ScriptableObject .asset files to extract type name and serialized field values.
    /// </summary>
    public class ScriptableObjectScanner : IAssetScanner
    {
        static readonly Regex ScriptGuidRegex = new(@"m_Script:\s*\{.*guid:\s*(\w+)");
        static readonly Regex FieldRegex = new(@"^\s{2}(\w+):\s*(.+)$", RegexOptions.Multiline);

        /// <inheritdoc />
        public AssetKind Kind => AssetKind.ScriptableObject;

        /// <inheritdoc />
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>
        /// True if the path ends with ".asset" (case-insensitive) and is not under ProjectSettings/.
        /// </returns>
        public bool CanScan(string assetPath)
        {
            if (assetPath == null) return false;
            if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;
            // Exclude ProjectSettings
            if (assetPath.Replace("\\", "/").Contains("ProjectSettings/")) return false;
            return true;
        }

        /// <inheritdoc />
        /// <param name="assetPath">Absolute or relative path to the .asset file.</param>
        /// <returns>
        /// An <see cref="IndexEntry"/> with the asset name, user-defined field symbols, script GUID
        /// dependency, and a compact summary; or <c>null</c> if the file is missing, empty, or not YAML.
        /// </returns>
        public IndexEntry Scan(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;

            var content = File.ReadAllText(assetPath);
            if (string.IsNullOrWhiteSpace(content)) return null;

            // Must be a YAML asset
            if (!content.StartsWith("%YAML")) return null;

            var assetName = Path.GetFileNameWithoutExtension(assetPath);
            var symbols = new List<string> { assetName };
            var dependencies = new List<string>();

            // Extract script GUID
            var guidMatch = ScriptGuidRegex.Match(content);
            if (guidMatch.Success)
                dependencies.Add(guidMatch.Groups[1].Value);

            // Extract top-level fields (skip Unity internals starting with m_)
            var fields = new List<string>();
            foreach (Match m in FieldRegex.Matches(content))
            {
                var fieldName = m.Groups[1].Value;
                if (!fieldName.StartsWith("m_") && fieldName != "m_Script")
                {
                    symbols.Add(fieldName);
                    fields.Add($"{fieldName}: {m.Groups[2].Value.Trim()}");
                }
            }

            var summary = $"{assetName} (ScriptableObject)";
            if (fields.Count > 0)
                summary += "\n  " + string.Join("\n  ", fields.Take(10));

            return new IndexEntry
            {
                AssetPath = assetPath,
                Kind = AssetKind.ScriptableObject,
                Name = assetName,
                Symbols = symbols.Distinct().ToArray(),
                Dependencies = dependencies.ToArray(),
                Summary = summary,
                LastModifiedTicks = new FileInfo(assetPath).LastWriteTimeUtc.Ticks
            };
        }
    }
}
