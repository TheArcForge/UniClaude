using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniClaude.Editor
{
    /// <summary>
    /// Scans C# script files (.cs) and extracts class hierarchy, public members,
    /// serialized fields, and dependencies using regex-based text parsing.
    /// </summary>
    public class ScriptScanner : IAssetScanner
    {
        // Matches: public/internal class/struct/interface/enum Name : Base, IFoo
        static readonly Regex TypeDeclarationRegex = new(
            @"(?:public|internal)\s+(?:abstract\s+|static\s+|sealed\s+|partial\s+)*(?:class|struct|interface|enum)\s+(\w+)(?:\s*(?:<[^>]+>))?\s*(?::\s*(.+?))?(?:\s*\{|\s*$)",
            RegexOptions.Multiline);

        // Matches: public ReturnType MethodName(params)
        static readonly Regex PublicMethodRegex = new(
            @"^\s*public\s+(?:(?:static|virtual|override|abstract|async)\s+)*\w[\w<>\[\],\s\?]*\s+(\w+)\s*\(",
            RegexOptions.Multiline);

        // Matches: public Type PropertyName { get; }
        static readonly Regex PublicPropertyRegex = new(
            @"^\s*public\s+(?:(?:static|virtual|override|abstract)\s+)*\w[\w<>\[\],\s\?]*\s+(\w+)\s*\{",
            RegexOptions.Multiline);

        // Matches: [SerializeField] Type name
        static readonly Regex SerializedFieldRegex = new(
            @"^\s*\[SerializeField\]\s*(?:private\s+|protected\s+)?\w[\w<>\[\],\s\?]*\s+(\w+)\s*[;=]",
            RegexOptions.Multiline);

        // Matches: public Type fieldName; or public Type fieldName = value;
        // Excludes keywords that would indicate a method/property/type declaration
        static readonly Regex PublicFieldRegex = new(
            @"^\s*public\s+(?!(?:class|struct|interface|enum|void|static\s+void|override|virtual|abstract|event)\b)\w[\w<>\[\],\s\?]*\s+(\w+)\s*[;=]",
            RegexOptions.Multiline);

        // Matches interface member methods (implicit public, no access modifier, ends with ;)
        // e.g.  void TakeDamage(float amount);
        static readonly Regex InterfaceMethodRegex = new(
            @"^\s+(?!public|private|protected|internal|static|abstract|virtual|override|readonly|new\b)\w[\w<>\[\],\s\?]*\s+(\w+)\s*\([^)]*\)\s*;",
            RegexOptions.Multiline);

        /// <inheritdoc />
        public AssetKind Kind => AssetKind.Script;

        /// <inheritdoc />
        /// <param name="assetPath">Relative asset path from project root.</param>
        /// <returns>True if the path ends with ".cs" (case-insensitive).</returns>
        public bool CanScan(string assetPath)
        {
            return assetPath != null && assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        /// <param name="assetPath">Absolute or relative path to the .cs file.</param>
        /// <returns>
        /// An <see cref="IndexEntry"/> with the extracted type name, symbols, dependencies,
        /// and a compact summary; or <c>null</c> if the file is empty or contains no type declarations.
        /// </returns>
        public IndexEntry Scan(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;

            var content = File.ReadAllText(assetPath);
            if (string.IsNullOrWhiteSpace(content)) return null;

            var typeMatch = TypeDeclarationRegex.Match(content);
            if (!typeMatch.Success) return null;

            var typeName = typeMatch.Groups[1].Value;
            var inheritance = typeMatch.Groups[2].Success ? typeMatch.Groups[2].Value.Trim() : null;

            var symbols = new List<string> { typeName };
            var dependencies = new List<string>();

            // Extract base types and interfaces from inheritance clause
            var baseTypes = new List<string>();
            if (!string.IsNullOrEmpty(inheritance))
            {
                baseTypes = inheritance.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => Regex.Replace(s, @"<.*>", "")) // strip generics
                    .ToList();
                dependencies.AddRange(baseTypes);
            }

            // Determine if the primary declaration is an interface (implicit-public members)
            var isInterface = typeMatch.Value.Contains("interface");

            // Extract public methods (skip constructors — same name as type)
            var methods = new List<string>();
            foreach (Match m in PublicMethodRegex.Matches(content))
            {
                var name = m.Groups[1].Value;
                if (name != typeName)
                {
                    symbols.Add(name);
                    methods.Add(name);
                }
            }

            // For interfaces, also capture implicitly-public member methods (no access modifier)
            if (isInterface)
            {
                foreach (Match m in InterfaceMethodRegex.Matches(content))
                {
                    var name = m.Groups[1].Value;
                    if (!methods.Contains(name) && name != typeName)
                    {
                        symbols.Add(name);
                        methods.Add(name);
                    }
                }
            }

            // Extract public properties
            var properties = new List<string>();
            foreach (Match m in PublicPropertyRegex.Matches(content))
            {
                var name = m.Groups[1].Value;
                symbols.Add(name);
                properties.Add(name);
            }

            // Extract [SerializeField] fields (any access modifier)
            var fields = new List<string>();
            foreach (Match m in SerializedFieldRegex.Matches(content))
            {
                var name = m.Groups[1].Value;
                symbols.Add(name);
                fields.Add(name);
            }

            // Extract public fields not already captured as properties or serialized fields
            foreach (Match m in PublicFieldRegex.Matches(content))
            {
                var name = m.Groups[1].Value;
                if (!fields.Contains(name) && !properties.Contains(name))
                {
                    symbols.Add(name);
                    fields.Add(name);
                }
            }

            var summary = BuildSummary(typeName, baseTypes, fields, methods, properties, dependencies);

            return new IndexEntry
            {
                AssetPath = assetPath,
                Kind = AssetKind.Script,
                Name = typeName,
                Symbols = symbols.Distinct().ToArray(),
                Dependencies = dependencies.ToArray(),
                Summary = summary,
                LastModifiedTicks = new FileInfo(assetPath).LastWriteTimeUtc.Ticks
            };
        }

        /// <summary>
        /// Builds a compact human-readable summary of a type's declaration, members, and dependencies.
        /// </summary>
        /// <param name="typeName">The primary type name.</param>
        /// <param name="baseTypes">Base class and implemented interfaces.</param>
        /// <param name="fields">Collected field names (serialized and public).</param>
        /// <param name="methods">Collected public method names.</param>
        /// <param name="properties">Collected public property names.</param>
        /// <param name="dependencies">All dependency type names (same as baseTypes for scripts).</param>
        /// <returns>A multiline summary string suitable for context injection.</returns>
        static string BuildSummary(
            string typeName,
            List<string> baseTypes,
            List<string> fields,
            List<string> methods,
            List<string> properties,
            List<string> dependencies)
        {
            var parts = new List<string>();

            // Type declaration line, e.g. "Player : MonoBehaviour (IDamageable)"
            var decl = typeName;
            if (baseTypes.Count > 0)
            {
                var baseClass = baseTypes[0];
                var interfaces = baseTypes.Skip(1).ToList();
                decl += $" : {baseClass}";
                if (interfaces.Count > 0)
                    decl += $" ({string.Join(", ", interfaces)})";
            }
            parts.Add(decl);

            if (fields.Count > 0)
                parts.Add($"  Fields: {string.Join(", ", fields)}");

            if (methods.Count > 0)
                parts.Add($"  Methods: {string.Join(", ", methods)}");

            if (properties.Count > 0)
                parts.Add($"  Properties: {string.Join(", ", properties)}");

            if (dependencies.Count > 0)
                parts.Add($"  Depends on: {string.Join(", ", dependencies)}");

            return string.Join("\n", parts);
        }
    }
}
