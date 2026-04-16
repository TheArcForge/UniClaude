using System;
using System.Collections.Generic;

namespace UniClaude.Editor
{
    /// <summary>
    /// The type of Unity asset an index entry represents.
    /// </summary>
    public enum AssetKind
    {
        /// <summary>C# script file (.cs).</summary>
        Script,
        /// <summary>Unity scene file (.unity).</summary>
        Scene,
        /// <summary>Unity prefab file (.prefab).</summary>
        Prefab,
        /// <summary>ScriptableObject asset (.asset).</summary>
        ScriptableObject,
        /// <summary>ProjectSettings file.</summary>
        ProjectSettings,
        /// <summary>Shader file (.shader).</summary>
        Shader,
        /// <summary>Other asset (metadata only).</summary>
        Asset
    }

    /// <summary>
    /// A single entry in the project index, representing one indexed asset.
    /// </summary>
    [Serializable]
    public class IndexEntry
    {
        /// <summary>Relative path from project root (e.g. "Assets/Scripts/Player.cs").</summary>
        public string AssetPath;

        /// <summary>The type of asset this entry represents.</summary>
        public AssetKind Kind;

        /// <summary>Short display name (e.g. "Player").</summary>
        public string Name;

        /// <summary>Symbols defined in this asset (class names, method names, field names).</summary>
        public string[] Symbols;

        /// <summary>Asset paths this entry depends on (base classes, referenced types).</summary>
        public string[] Dependencies;

        /// <summary>Compact text summary used for context injection.</summary>
        public string Summary;

        /// <summary>File modification time in ticks, for staleness detection.</summary>
        public long LastModifiedTicks;

        /// <summary>
        /// Origin of this entry. Null or empty for project assets,
        /// otherwise the package name (e.g. "com.arcforge.ui").
        /// </summary>
        public string Source;
    }

    /// <summary>
    /// Container for the full project index.
    /// </summary>
    [Serializable]
    public class ProjectIndex
    {
        /// <summary>All indexed entries.</summary>
        public List<IndexEntry> Entries = new List<IndexEntry>();

        /// <summary>Name of the Unity project.</summary>
        public string ProjectName;

        /// <summary>Unity Editor version string.</summary>
        public string UnityVersion;

        /// <summary>
        /// When the last full-depth scan was performed (ISO 8601 string).
        /// Stored as string (not DateTime) for clean JSON serialization, consistent
        /// with ChatMessage.Timestamp pattern. Spec says DateTime but string is a
        /// deliberate codebase-consistency choice.
        /// </summary>
        public string LastFullScan;

        /// <summary>Compact folder tree summary for the always-inject context tier.</summary>
        public string ProjectTreeSummary;

        /// <summary>Metadata about packages that were included in the index.</summary>
        public List<PackageInfo> IndexedPackages = new List<PackageInfo>();
    }

    /// <summary>
    /// Metadata about an installed Unity package discovered during indexing.
    /// </summary>
    [Serializable]
    public class PackageInfo
    {
        /// <summary>Package identifier (e.g. "com.arcforge.ui").</summary>
        public string Name;

        /// <summary>Package version string (e.g. "0.4.0").</summary>
        public string Version;

        /// <summary>Human-readable package name (e.g. "ArcForge UI").</summary>
        public string DisplayName;

        /// <summary>True if embedded in Packages/ folder, false if from registry cache.</summary>
        public bool IsLocal;

        /// <summary>Absolute path to the package on disk.</summary>
        public string ResolvedPath;
    }

    /// <summary>
    /// Result of a context retrieval operation, containing both the formatted
    /// prompt for the CLI and display data for the UI.
    /// </summary>
    public class ContextResult
    {
        /// <summary>Full system prompt string to pass via --system-prompt.</summary>
        public string FormattedPrompt;

        /// <summary>UI display data for the context block above the user's message.</summary>
        public ContextBlock Block;
    }

    /// <summary>
    /// Display data for the collapsible context block shown above user messages.
    /// </summary>
    public class ContextBlock
    {
        /// <summary>Short summary line (e.g. "1,847 tokens — 3 files").</summary>
        public string Summary;

        /// <summary>Estimated token count of the injected context.</summary>
        public int TokenCount;

        /// <summary>Names of files included in the context.</summary>
        public List<string> FileNames;

        /// <summary>Full formatted context text (shown when the block is expanded).</summary>
        public string FullText;
    }
}
