using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UniClaude.Editor
{
    /// <summary>
    /// Formats project index data and retrieval results into a system prompt string
    /// for injection via <c>--system-prompt</c>. Produces two tiers:
    /// Tier 1 (always): project summary. Tier 2 (when matches): relevant file details.
    /// </summary>
    public static class ContextFormatter
    {
        /// <summary>
        /// Formats the project index and retrieval results into a context result
        /// containing both the system prompt string and UI display data.
        /// </summary>
        /// <param name="index">The project index (for Tier 1 summary).</param>
        /// <param name="result">The retrieval results (for Tier 2 details). May be empty.</param>
        /// <returns>A <see cref="ContextResult"/> with formatted prompt and display block, or null if index is null.</returns>
        public static ContextResult Format(ProjectIndex index, RetrievalResult result)
        {
            if (index == null) return null;

            var sb = new StringBuilder();

            // Tier 1: Project Summary (always)
            sb.AppendLine("=== Project Context ===");
            sb.Append("Project: ").Append(index.ProjectName ?? "Unknown");
            if (!string.IsNullOrEmpty(index.UnityVersion))
                sb.Append(" (Unity ").Append(index.UnityVersion).Append(')');
            sb.AppendLine();

            // Stats by kind
            var scriptCount = index.Entries.Count(e => e.Kind == AssetKind.Script);
            var sceneCount = index.Entries.Count(e => e.Kind == AssetKind.Scene);
            var prefabCount = index.Entries.Count(e => e.Kind == AssetKind.Prefab);
            sb.Append("Scripts: ").Append(scriptCount);
            sb.Append(" | Scenes: ").Append(sceneCount);
            sb.Append(" | Prefabs: ").Append(prefabCount);
            sb.AppendLine();

            // Indexed packages
            if (index.IndexedPackages != null && index.IndexedPackages.Count > 0)
            {
                sb.Append("Packages: ");
                sb.AppendLine(string.Join(", ", index.IndexedPackages.Select(p => $"{p.Name} ({p.Version})")));
            }

            // Project tree
            if (!string.IsNullOrEmpty(index.ProjectTreeSummary))
            {
                sb.AppendLine();
                sb.AppendLine(index.ProjectTreeSummary);
            }

            // Tier 2: Relevant Files (only when matches exist)
            var fileNames = new List<string>();
            if (result?.Entries != null && result.Entries.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Relevant Files ===");

                foreach (var entry in result.Entries)
                {
                    sb.Append("# ").Append(System.IO.Path.GetFileName(entry.AssetPath));
                    sb.Append(" (").Append(entry.AssetPath).AppendLine(")");
                    sb.AppendLine(entry.Summary);
                    sb.AppendLine();
                    fileNames.Add(System.IO.Path.GetFileName(entry.AssetPath));
                }
            }

            var prompt = sb.ToString().TrimEnd();
            var tokenCount = prompt.Length / 4 + 1;

            var block = new ContextBlock
            {
                TokenCount = tokenCount,
                FileNames = fileNames,
                FullText = prompt
            };

            if (fileNames.Count > 0)
            {
                var fileList = string.Join(", ", fileNames.Take(5));
                if (fileNames.Count > 5)
                    fileList += $" +{fileNames.Count - 5} more";
                block.Summary = $"{tokenCount:N0} tokens \u2014 {fileNames.Count} file{(fileNames.Count == 1 ? "" : "s")} ({fileList})";
            }
            else
            {
                block.Summary = $"{tokenCount:N0} tokens \u2014 project summary only";
            }

            return new ContextResult
            {
                FormattedPrompt = prompt,
                Block = block
            };
        }

        /// <summary>
        /// Formats only the Tier 1 project summary (name, version, stats, tree).
        /// Used for the lightweight system prompt injection.
        /// </summary>
        /// <param name="index">The project index.</param>
        /// <returns>The Tier 1 summary string, or null if index is null.</returns>
        public static string FormatTier1(ProjectIndex index)
        {
            if (index == null) return null;

            var sb = new StringBuilder();

            sb.AppendLine("The following is background project context for reference. Use it to inform your answers but do not change your response style — continue responding as a concise coding assistant.");
            sb.AppendLine();
            sb.AppendLine("=== Project Context ===");
            sb.Append("Project: ").Append(index.ProjectName ?? "Unknown");
            if (!string.IsNullOrEmpty(index.UnityVersion))
                sb.Append(" (Unity ").Append(index.UnityVersion).Append(')');
            sb.AppendLine();

            var scriptCount = index.Entries.Count(e => e.Kind == AssetKind.Script);
            var sceneCount = index.Entries.Count(e => e.Kind == AssetKind.Scene);
            var prefabCount = index.Entries.Count(e => e.Kind == AssetKind.Prefab);
            sb.Append("Scripts: ").Append(scriptCount);
            sb.Append(" | Scenes: ").Append(sceneCount);
            sb.Append(" | Prefabs: ").Append(prefabCount);
            sb.AppendLine();

            // Indexed packages
            if (index.IndexedPackages != null && index.IndexedPackages.Count > 0)
            {
                sb.Append("Packages: ");
                sb.AppendLine(string.Join(", ", index.IndexedPackages.Select(p => $"{p.Name} ({p.Version})")));
            }

            if (!string.IsNullOrEmpty(index.ProjectTreeSummary))
            {
                sb.AppendLine();
                sb.AppendLine(index.ProjectTreeSummary);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Formats only the Tier 2 relevant files section from retrieval results.
        /// Used by the <c>project_search</c> MCP tool.
        /// </summary>
        /// <param name="result">The retrieval results.</param>
        /// <returns>Formatted file details, or a "no matches" message if empty.</returns>
        public static string FormatResults(RetrievalResult result)
        {
            if (result?.Entries == null || result.Entries.Count == 0)
                return "No matching files found in the project index.";

            var sb = new StringBuilder();
            sb.AppendLine("=== Relevant Files ===");

            foreach (var entry in result.Entries)
            {
                sb.Append("# ").Append(System.IO.Path.GetFileName(entry.AssetPath));
                sb.Append(" (").Append(entry.AssetPath).AppendLine(")");
                sb.AppendLine(entry.Summary);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}
