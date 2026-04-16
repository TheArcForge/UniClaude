using UnityEditor;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Domain reload strategy options.
    /// </summary>
    public enum ReloadStrategy
    {
        /// <summary>Auto-lock on first tool call, unlock on turn end.</summary>
        Auto = 0,
        /// <summary>Manual lock/unlock via BeginScriptEditing/EndScriptEditing tools.</summary>
        Manual = 1
    }

    /// <summary>
    /// Persists MCP server settings via EditorPrefs.
    /// Uses EditorPrefs (not file-based JSON) because these settings must survive
    /// domain reload immediately — SessionState and EditorPrefs persist through
    /// managed domain teardown.
    /// </summary>
    public class MCPSettings
    {
        readonly string _prefix;

        /// <summary>
        /// Creates MCP settings with the given EditorPrefs key prefix.
        /// </summary>
        /// <param name="prefix">Prefix for EditorPrefs keys.</param>
        public MCPSettings(string prefix = "UniClaude_MCP_")
        {
            _prefix = prefix;
        }

        /// <summary>Gets or sets the HTTP server port. 0 = OS-assigned.</summary>
        public int Port
        {
            get => EditorPrefs.GetInt(_prefix + "Port", 0);
            set => EditorPrefs.SetInt(_prefix + "Port", value);
        }

        /// <summary>Gets or sets whether the MCP server is enabled.</summary>
        public bool Enabled
        {
            get => EditorPrefs.GetBool(_prefix + "Enabled", true);
            set => EditorPrefs.SetBool(_prefix + "Enabled", value);
        }

        /// <summary>Gets or sets whether the server auto-starts on Editor launch.</summary>
        public bool AutoStart
        {
            get => EditorPrefs.GetBool(_prefix + "AutoStart", true);
            set => EditorPrefs.SetBool(_prefix + "AutoStart", value);
        }

        /// <summary>Gets or sets the log level (0=None, 1=Info, 2=Debug).</summary>
        public int LogLevel
        {
            get => EditorPrefs.GetInt(_prefix + "LogLevel", 1);
            set => EditorPrefs.SetInt(_prefix + "LogLevel", value);
        }

        /// <summary>Gets or sets the domain reload strategy.</summary>
        public ReloadStrategy DomainReloadStrategy
        {
            get => (ReloadStrategy)EditorPrefs.GetInt(_prefix + "ReloadStrategy", 0);
            set => EditorPrefs.SetInt(_prefix + "ReloadStrategy", (int)value);
        }

        /// <summary>Gets or sets the safety timeout in seconds for auto-unlock.</summary>
        public int ReloadTimeoutSeconds
        {
            get => EditorPrefs.GetInt(_prefix + "ReloadTimeout", 120);
            set => EditorPrefs.SetInt(_prefix + "ReloadTimeout", value);
        }

        /// <summary>Gets or sets whether verbose tool activity logging is enabled in chat.</summary>
        public bool VerboseToolLogging
        {
            get => EditorPrefs.GetBool(_prefix + "VerboseLogging", false);
            set => EditorPrefs.SetBool(_prefix + "VerboseLogging", value);
        }

        /// <summary>
        /// Clears all MCP settings, resetting to defaults.
        /// </summary>
        public void ClearAll()
        {
            foreach (var key in new[] { "Port", "Enabled", "AutoStart", "LogLevel",
                "ReloadStrategy", "ReloadTimeout", "VerboseLogging" })
            {
                EditorPrefs.DeleteKey(_prefix + key);
            }
        }
    }
}
