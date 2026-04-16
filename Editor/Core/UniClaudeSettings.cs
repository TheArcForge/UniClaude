using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UniClaude.Editor
{
    /// <summary>
    /// Persistent user preferences for UniClaude. Stored in Library/UniClaude/settings.json.
    /// </summary>
    [Serializable]
    public class UniClaudeSettings
    {
        /// <summary>The selected model identifier (e.g. "sonnet", "opus").</summary>
        public string SelectedModel;

        /// <summary>The selected reasoning effort level (e.g. "low", "medium", "high").</summary>
        public string SelectedEffort = "high";

        /// <summary>Chat font size preset. Valid values: "small", "medium", "large", "xlarge".</summary>
        public string ChatFontSize = "medium";

        /// <summary>Whether project awareness context injection is enabled.</summary>
        public bool ProjectAwarenessEnabled = true;

        /// <summary>
        /// User overrides for package indexing. Only stores packages where the user
        /// has explicitly changed from the default (local=on, registry=off).
        /// Key: package name, Value: true=include, false=exclude.
        /// </summary>
        public Dictionary<string, bool> PackageIndexOverrides = new Dictionary<string, bool>();

        /// <summary>
        /// Project folder paths excluded from indexing (relative from project root).
        /// e.g. "Assets/ThirdParty", "Assets/Plugins/SomeSDK"
        /// </summary>
        public List<string> ExcludedFolders = new List<string>();

        /// <summary>Port for the Node.js sidecar process to connect on. 0 = auto-assign a random port.</summary>
        public int SidecarPort;

        /// <summary>Path to the Node.js executable. Empty string = auto-detect from PATH.</summary>
        public string NodePath = "";

        /// <summary>When true, only error-level sidecar logs appear in the Unity Console.</summary>
        public bool VerboseLogging = false;

        /// <summary>When true, UniClaude MCP tools are auto-approved without prompting.</summary>
        public bool AutoAllowMCPTools = true;

        /// <summary>
        /// Maximum token budget for the project tree summary in Tier 1 context.
        /// The tree is expanded breadth-first until this budget is reached.
        /// 0 = unlimited (sends the full tree — may be expensive on large projects).
        /// Default: 3300 (~$0.01 per message at Sonnet input pricing).
        /// </summary>
        public int ContextTokenBudget = 3300;

        /// <summary>
        /// Available model choices — single source of truth for Settings view and /model command.
        /// </summary>
        public static readonly List<(string Value, string Label, string Description)> ModelChoices = new()
        {
            ("sonnet", "Sonnet 4.6", "Best for everyday tasks"),
            ("opus", "Opus 4.6", "Most capable for complex work"),
            ("haiku", "Haiku 4.5", "Fastest for quick answers"),
        };

        /// <summary>
        /// Available reasoning effort choices — controls how much thinking the model uses.
        /// </summary>
        public static readonly List<(string Value, string Label, string Description)> EffortChoices = new()
        {
            ("low", "Low", "Quick responses, minimal reasoning"),
            ("medium", "Medium", "Balanced speed and depth"),
            ("high", "High", "Deep reasoning, slower responses"),
            ("max", "Max", "Deepest reasoning, Opus only"),
        };

        static string _settingsDir = Path.Combine("Library", "UniClaude");

        /// <summary>Override the settings directory (for testing).</summary>
        public static string SettingsDir
        {
            get => _settingsDir;
            set => _settingsDir = value;
        }

        static string SettingsPath => Path.Combine(_settingsDir, "settings.json");

        /// <summary>Resets the settings directory to the default path.</summary>
        public static void ResetSettingsDir()
        {
            _settingsDir = Path.Combine("Library", "UniClaude");
        }

        /// <summary>
        /// Loads settings from disk. Returns defaults if file is missing or corrupt.
        /// </summary>
        /// <returns>The loaded or default settings.</returns>
        public static UniClaudeSettings Load()
        {
            if (!File.Exists(SettingsPath)) return new UniClaudeSettings();

            try
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<UniClaudeSettings>(json) ?? new UniClaudeSettings();
            }
            catch
            {
                return new UniClaudeSettings();
            }
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        /// <param name="settings">The settings to persist.</param>
        public static void Save(UniClaudeSettings settings)
        {
            if (!Directory.Exists(_settingsDir))
                Directory.CreateDirectory(_settingsDir);

            var tmpPath = SettingsPath + ".tmp";
            File.WriteAllText(tmpPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            File.Move(tmpPath, SettingsPath);
        }
    }
}
