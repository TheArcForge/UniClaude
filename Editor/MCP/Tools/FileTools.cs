using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for file system operations: read, write, create, modify, delete, and search.
    /// All paths are resolved relative to the Unity project root.
    /// </summary>
    public static class FileTools
    {
        /// <summary>
        /// Gets the absolute path to the Unity project root directory.
        /// </summary>
        static string ProjectRoot => PathSandbox.ProjectRoot;

        /// <summary>
        /// Suggests files in the same directory that have similar names to the target,
        /// sorted by Levenshtein distance (most similar first).
        /// </summary>
        /// <param name="path">The absolute path of the file that was not found.</param>
        /// <param name="max">Maximum number of suggestions to return.</param>
        /// <returns>Array of similar filenames found in the parent directory.</returns>
        static string[] SuggestSimilarFiles(string path, int max = 5)
        {
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return Array.Empty<string>();

            var targetName = Path.GetFileName(path);
            var files = Directory.GetFiles(dir);

            return files
                .Select(f => Path.GetFileName(f))
                .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => LevenshteinDistance(targetName.ToLowerInvariant(), f.ToLowerInvariant()))
                .Take(max)
                .ToArray();
        }

        /// <summary>
        /// Computes the Levenshtein edit distance between two strings.
        /// </summary>
        /// <param name="a">The first string.</param>
        /// <param name="b">The second string.</param>
        /// <returns>The minimum number of single-character edits to transform a into b.</returns>
        static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var matrix = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= b.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[a.Length, b.Length];
        }

        /// <summary>
        /// Reads the contents of a file at the given project-relative path.
        /// Returns the file content and line count on success.
        /// If the file is not found, suggests similar filenames in the same directory.
        /// </summary>
        /// <param name="path">The file path relative to the project root.</param>
        /// <returns>Success with file content and line count, or error with suggestions.</returns>
        [MCPTool("file_read", "Read the contents of a file. Returns content and line count.")]
        public static MCPToolResult ReadFile(
            [MCPToolParam("File path relative to project root", required: true)]
            string path)
        {
            var absolutePath = PathSandbox.Resolve(path);

            if (!File.Exists(absolutePath))
            {
                var suggestions = SuggestSimilarFiles(absolutePath);
                var message = $"File not found: {path}";
                if (suggestions.Length > 0)
                {
                    var dir = Path.GetDirectoryName(path) ?? "";
                    var suggestionPaths = suggestions.Select(s =>
                        string.IsNullOrEmpty(dir) ? s : Path.Combine(dir, s));
                    message += $"\n\nDid you mean one of these?\n  " +
                               string.Join("\n  ", suggestionPaths);
                }
                return MCPToolResult.Error(message);
            }

            var content = File.ReadAllText(absolutePath);
            var lineCount = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;

            return MCPToolResult.Success(new
            {
                path = path,
                content = content,
                lineCount = lineCount
            });
        }

        /// <summary>
        /// Writes content to a file at the given project-relative path.
        /// Creates parent directories if they do not exist.
        /// Overwrites the file if it already exists.
        /// </summary>
        /// <param name="path">The file path relative to the project root.</param>
        /// <param name="content">The content to write to the file.</param>
        /// <returns>Success with the number of bytes written.</returns>
        [MCPTool("file_write", "Write or overwrite a file. Creates parent directories if needed.")]
        public static MCPToolResult WriteFile(
            [MCPToolParam("File path relative to project root", required: true)]
            string path,
            [MCPToolParam("Content to write to the file", required: true)]
            string content)
        {
            var absolutePath = PathSandbox.ResolveWritable(path);
            var dir = Path.GetDirectoryName(absolutePath);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(absolutePath, content);
            var byteCount = new FileInfo(absolutePath).Length;

            return MCPToolResult.Success(new
            {
                path = path,
                bytesWritten = byteCount
            });
        }

        /// <summary>
        /// Creates a C# script file from a template or custom content.
        /// Supported templates: "MonoBehaviour", "ScriptableObject", "class", "custom".
        /// If template is "custom", the content parameter is written directly.
        /// Calls AssetDatabase.Refresh() after creation.
        /// </summary>
        /// <param name="path">The .cs file path relative to the project root.</param>
        /// <param name="template">The template type: "MonoBehaviour", "ScriptableObject", "class", or "custom".</param>
        /// <param name="content">Custom script content. Used only when template is "custom".</param>
        /// <returns>Success with the created file path.</returns>
        [MCPTool("file_create_script", "Create a C# script from a template (MonoBehaviour, ScriptableObject, class) or custom content.")]
        public static MCPToolResult CreateScript(
            [MCPToolParam("File path relative to project root (must end in .cs)", required: true)]
            string path,
            [MCPToolParam("Template type: MonoBehaviour, ScriptableObject, class, or custom", required: true)]
            string template,
            [MCPToolParam("Custom script content (used when template is 'custom')", required: false)]
            string content)
        {
            var absolutePath = PathSandbox.ResolveWritable(path);
            var dir = Path.GetDirectoryName(absolutePath);
            var className = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string scriptContent;
            switch (template.ToLowerInvariant())
            {
                case "monobehaviour":
                    scriptContent = GenerateMonoBehaviourTemplate(className);
                    break;
                case "scriptableobject":
                    scriptContent = GenerateScriptableObjectTemplate(className);
                    break;
                case "class":
                    scriptContent = GenerateClassTemplate(className);
                    break;
                case "custom":
                    scriptContent = content ?? "";
                    break;
                default:
                    return MCPToolResult.Error(
                        $"Unknown template '{template}'. Valid templates: MonoBehaviour, ScriptableObject, class, custom");
            }

            File.WriteAllText(absolutePath, scriptContent);
            AssetDatabase.Refresh();

            return MCPToolResult.Success(new
            {
                path = path,
                template = template,
                className = className
            });
        }

        /// <summary>
        /// Performs a find-and-replace within a C# script file.
        /// Returns a diff preview with context around the change.
        /// Errors if the old string is not found, showing a snippet of the file.
        /// </summary>
        /// <param name="path">The .cs file path relative to the project root.</param>
        /// <param name="oldString">The exact text to find in the file.</param>
        /// <param name="newString">The replacement text.</param>
        /// <returns>Success with a diff preview, or error with file snippet.</returns>
        [MCPTool("file_modify_script", "Find and replace text within a C# script file. Returns a diff preview.")]
        public static MCPToolResult ModifyScript(
            [MCPToolParam("File path relative to project root", required: true)]
            string path,
            [MCPToolParam("Exact text to find in the file", required: true)]
            string oldString,
            [MCPToolParam("Replacement text", required: true)]
            string newString)
        {
            var absolutePath = PathSandbox.ResolveWritable(path);

            if (!File.Exists(absolutePath))
                return MCPToolResult.Error($"File not found: {path}");

            var originalContent = File.ReadAllText(absolutePath);

            if (!originalContent.Contains(oldString))
            {
                var snippet = originalContent.Length > 500
                    ? originalContent.Substring(0, 500) + "\n... (truncated)"
                    : originalContent;
                return MCPToolResult.Error(
                    $"oldString not found in {path}.\n\nFile content:\n{snippet}");
            }

            var newContent = originalContent.Replace(oldString, newString);
            File.WriteAllText(absolutePath, newContent);

            var diff = BuildDiffPreview(originalContent, newContent, oldString, newString);

            return MCPToolResult.Success(new
            {
                path = path,
                diff = diff
            });
        }

        /// <summary>
        /// Deletes a file and its associated .meta file (if one exists).
        /// Errors if the file does not exist.
        /// </summary>
        /// <param name="path">The file path relative to the project root.</param>
        /// <returns>Success confirming deletion, or error if the file does not exist.</returns>
        [MCPTool("file_delete", "Delete a file and its .meta file if present.")]
        public static MCPToolResult DeleteFile(
            [MCPToolParam("File path relative to project root", required: true)]
            string path)
        {
            var absolutePath = PathSandbox.ResolveWritable(path);

            if (!File.Exists(absolutePath))
                return MCPToolResult.Error($"File not found: {path}");

            File.Delete(absolutePath);

            var metaPath = absolutePath + ".meta";
            var metaDeleted = false;
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
                metaDeleted = true;
            }

            return MCPToolResult.Success(new
            {
                path = path,
                deleted = true,
                metaDeleted = metaDeleted
            });
        }

        /// <summary>
        /// Searches for files matching a glob pattern within the project.
        /// Supports ** for recursive directory matching and * for wildcard filename matching.
        /// Results are capped at 100 entries and returned as project-relative paths.
        /// </summary>
        /// <param name="pattern">Glob pattern (e.g. "Assets/**/*.cs", "*.json").</param>
        /// <returns>Success with an array of matching project-relative paths.</returns>
        [MCPTool("file_find", "Search for files matching a glob pattern. Supports ** and * wildcards. Max 100 results.")]
        public static MCPToolResult FindFiles(
            [MCPToolParam("Glob pattern (e.g. 'Assets/**/*.cs', '*.json')", required: true)]
            string pattern)
        {
            var root = PathSandbox.ProjectRoot;
            var matches = GlobSearch(root, pattern);
            var relativePaths = matches
                .Select(PathSandbox.MakeRelative)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToArray();

            return MCPToolResult.Success(new
            {
                pattern = pattern,
                matches = relativePaths,
                count = relativePaths.Length,
                truncated = matches.Count > 100
            });
        }

        /// <summary>
        /// Performs a glob-style file search, supporting ** for recursive matching
        /// and * for single-segment wildcards.
        /// </summary>
        /// <param name="root">The root directory to search from.</param>
        /// <param name="pattern">The glob pattern.</param>
        /// <returns>List of matching absolute file paths.</returns>
        static List<string> GlobSearch(string root, string pattern)
        {
            // Normalize separators
            pattern = pattern.Replace('\\', '/');

            // Split into directory prefix and filename pattern
            var parts = pattern.Split('/');
            var results = new List<string>();

            GlobSearchRecursive(root, parts, 0, results);
            return results;
        }

        /// <summary>
        /// Recursively matches path segments against glob pattern parts.
        /// </summary>
        /// <param name="currentDir">The current directory being searched.</param>
        /// <param name="parts">The glob pattern split into path segments.</param>
        /// <param name="partIndex">The current segment index being matched.</param>
        /// <param name="results">The accumulator list for matching paths.</param>
        static void GlobSearchRecursive(string currentDir, string[] parts, int partIndex, List<string> results)
        {
            if (!Directory.Exists(currentDir))
                return;

            // If we've consumed all parts, no match (we need a file at the end)
            if (partIndex >= parts.Length)
                return;

            var part = parts[partIndex];
            var isLast = partIndex == parts.Length - 1;

            if (part == "**")
            {
                // ** matches zero or more directories
                // Try matching the rest from the current directory (zero match)
                if (partIndex + 1 < parts.Length)
                {
                    GlobSearchRecursive(currentDir, parts, partIndex + 1, results);
                }

                // Try matching ** in each subdirectory (one or more match)
                try
                {
                    foreach (var dir in Directory.GetDirectories(currentDir))
                    {
                        GlobSearchRecursive(dir, parts, partIndex, results);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
            else if (isLast)
            {
                // Last segment: match files
                var regexPattern = GlobPartToRegex(part);
                try
                {
                    foreach (var file in Directory.GetFiles(currentDir))
                    {
                        var fileName = Path.GetFileName(file);
                        if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                            results.Add(file);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
            else
            {
                // Intermediate segment: match directories
                var regexPattern = GlobPartToRegex(part);
                try
                {
                    foreach (var dir in Directory.GetDirectories(currentDir))
                    {
                        var dirName = Path.GetFileName(dir);
                        if (Regex.IsMatch(dirName, regexPattern, RegexOptions.IgnoreCase))
                            GlobSearchRecursive(dir, parts, partIndex + 1, results);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>
        /// Converts a single glob segment (e.g. "*.cs") to a regex pattern.
        /// </summary>
        /// <param name="globPart">The glob segment.</param>
        /// <returns>A regex pattern string anchored at both ends.</returns>
        static string GlobPartToRegex(string globPart)
        {
            var escaped = Regex.Escape(globPart);
            // \* in the escaped string represents a literal * in the glob
            escaped = escaped.Replace("\\*", ".*");
            // \? in the escaped string represents a literal ? in the glob
            escaped = escaped.Replace("\\?", ".");
            return "^" + escaped + "$";
        }

        /// <summary>
        /// Builds a unified-diff-style preview showing context around a text replacement.
        /// </summary>
        /// <param name="original">The original file content.</param>
        /// <param name="modified">The modified file content.</param>
        /// <param name="oldString">The text that was replaced.</param>
        /// <param name="newString">The replacement text.</param>
        /// <returns>A string showing the change with surrounding context lines.</returns>
        static string BuildDiffPreview(string original, string modified, string oldString, string newString)
        {
            var originalLines = original.Split('\n');
            var sb = new StringBuilder();

            // Find the first line containing the old string
            int matchLine = -1;
            for (int i = 0; i < originalLines.Length; i++)
            {
                if (originalLines[i].Contains(oldString) ||
                    (i > 0 && string.Join("\n", originalLines.Skip(Math.Max(0, i - 1))).StartsWith(oldString)))
                {
                    matchLine = i;
                    break;
                }
            }

            // If it spans multiple lines, find the start line by accumulating content
            if (matchLine < 0)
            {
                var accumulated = "";
                for (int i = 0; i < originalLines.Length; i++)
                {
                    accumulated += (i > 0 ? "\n" : "") + originalLines[i];
                    if (accumulated.Contains(oldString))
                    {
                        // Walk back to find the starting line of the match
                        var preMatch = accumulated.Substring(0, accumulated.IndexOf(oldString));
                        matchLine = preMatch.Split('\n').Length - 1 + (i - (accumulated.Split('\n').Length - 1));
                        matchLine = Math.Max(0, matchLine);
                        break;
                    }
                }
            }

            if (matchLine < 0) matchLine = 0;

            const int contextLines = 3;
            int start = Math.Max(0, matchLine - contextLines);
            int end = Math.Min(originalLines.Length, matchLine + contextLines + oldString.Split('\n').Length);

            sb.AppendLine($"@@ -{start + 1},{end - start} @@");

            for (int i = start; i < end && i < originalLines.Length; i++)
            {
                var line = originalLines[i];
                if (line.Contains(oldString) || oldString.Contains(line.Trim()))
                    sb.AppendLine($"- {line}");
                else
                    sb.AppendLine($"  {line}");
            }

            // Show the new string lines
            var newLines = newString.Split('\n');
            foreach (var line in newLines)
            {
                sb.AppendLine($"+ {line}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a MonoBehaviour script template with the given class name.
        /// </summary>
        /// <param name="className">The name for the generated class.</param>
        /// <returns>The full C# script content.</returns>
        static string GenerateMonoBehaviourTemplate(string className)
        {
            return $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    void Start()
    {{
    }}

    void Update()
    {{
    }}
}}
";
        }

        /// <summary>
        /// Generates a ScriptableObject script template with the given class name.
        /// </summary>
        /// <param name="className">The name for the generated class.</param>
        /// <returns>The full C# script content.</returns>
        static string GenerateScriptableObjectTemplate(string className)
        {
            return $@"using UnityEngine;

[CreateAssetMenu(fileName = ""{className}"", menuName = ""ScriptableObjects/{className}"")]
public class {className} : ScriptableObject
{{
}}
";
        }

        /// <summary>
        /// Generates a plain class script template with the given class name.
        /// </summary>
        /// <param name="className">The name for the generated class.</param>
        /// <returns>The full C# script content.</returns>
        static string GenerateClassTemplate(string className)
        {
            return $@"using System;

public class {className}
{{
}}
";
        }
    }
}
