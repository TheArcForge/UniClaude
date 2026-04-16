using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniClaude.Editor
{
    /// <summary>
    /// Scans ProjectSettings/*.asset files to extract key project configuration
    /// (tags, layers, quality levels, input axes, product name).
    /// </summary>
    public class ProjectSettingsScanner : IAssetScanner
    {
        static readonly Regex FieldRegex = new(@"^\s{2}(\w+):\s*(.+)$", RegexOptions.Multiline);
        static readonly Regex TagRegex = new(@"^\s*- (\w.+)$", RegexOptions.Multiline);

        /// <inheritdoc />
        public AssetKind Kind => AssetKind.ProjectSettings;

        /// <inheritdoc />
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>
        /// True if the path is under ProjectSettings/ and ends with ".asset" (case-insensitive).
        /// </returns>
        public bool CanScan(string assetPath)
        {
            if (assetPath == null) return false;
            var normalized = assetPath.Replace("\\", "/");
            return normalized.Contains("ProjectSettings/") &&
                   normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        /// <param name="assetPath">Absolute or relative path to the ProjectSettings .asset file.</param>
        /// <returns>
        /// An <see cref="IndexEntry"/> with the settings file name, extracted field symbols,
        /// and a compact summary; or <c>null</c> if the file is missing or empty.
        /// </returns>
        public IndexEntry Scan(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;

            var content = File.ReadAllText(assetPath);
            if (string.IsNullOrWhiteSpace(content)) return null;

            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var symbols = new List<string> { fileName };
            var fields = new List<string>();

            foreach (Match m in FieldRegex.Matches(content))
            {
                var fieldName = m.Groups[1].Value;
                var value = m.Groups[2].Value.Trim();
                if (!fieldName.StartsWith("m_") || fieldName == "m_CompanyName" || fieldName == "m_ProductName")
                {
                    symbols.Add(fieldName);
                    if (value.Length < 80)
                        fields.Add($"{fieldName}: {value}");
                }
            }

            var summary = $"{fileName} (ProjectSettings)";
            if (fields.Count > 0)
                summary += "\n  " + string.Join("\n  ", fields.Take(15));

            return new IndexEntry
            {
                AssetPath = assetPath,
                Kind = AssetKind.ProjectSettings,
                Name = fileName,
                Symbols = symbols.Distinct().ToArray(),
                Dependencies = Array.Empty<string>(),
                Summary = summary,
                LastModifiedTicks = new FileInfo(assetPath).LastWriteTimeUtc.Ticks
            };
        }
    }
}
