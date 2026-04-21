using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniClaude.Editor;
using UniClaude.Editor.Installer;
using UniClaude.Editor.MCP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// A <see cref="VisualElement"/> that owns all settings UI for UniClaude.
    /// Extracts font size, model, effort, cache, project awareness, package indexing,
    /// folder filtering, MCP server, agent sidecar, session trust, and about sections
    /// from the monolithic UniClaudeWindow. Fires events for changes that the host window
    /// needs to handle; receives service references via <see cref="Refresh"/>.
    /// </summary>
    public class SettingsPanel : VisualElement
    {
        // ── Theme ─────────────────────────────────────────────────────────────

        readonly ThemeContext _theme;

        // ── Stored settings reference ─────────────────────────────────────────

        UniClaudeSettings _settings;

        // ── Stored service references (set in Refresh, used by helpers) ───────

        ProjectAwareness _projectAwareness;
        SidecarManager _sidecar;
        MCPServer _mcpServer;

        // ── Stored model/effort (for re-calling Refresh after self-changes) ───

        string _currentModel;
        string _currentEffort;

        // ── Foldout expansion state (persists across Refresh calls) ───────────

        bool _pkgFoldoutExpanded;
        bool _localPkgFoldoutExpanded;
        bool _registryPkgFoldoutExpanded;
        bool _folderFoldoutExpanded;

        // ── Rebuild banner (shown when package/folder filters change) ─────────

        VisualElement _rebuildBanner;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the user selects a new font size preset.
        /// The string argument is the preset key ("small", "medium", "large", "xlarge").
        /// </summary>
        public event Action<string> OnFontSizeChanged;

        /// <summary>
        /// Fired when the user selects a new model. The string argument is the model value.
        /// </summary>
        public event Action<string> OnModelChanged;

        /// <summary>
        /// Fired when the user selects a new reasoning effort level. The string argument is the effort value.
        /// </summary>
        public event Action<string> OnEffortChanged;

        /// <summary>
        /// Fired when any setting is changed that does not trigger a more specific event
        /// (e.g., node path, verbose logging, MCP lifecycle changes).
        /// </summary>
        public event Action OnSettingsChanged;

        /// <summary>
        /// Fired when the user toggles the Project Awareness enabled state.
        /// </summary>
        public event Action OnProjectAwarenessToggled;

        /// <summary>
        /// Fired when the user clicks "Full Index Rebuild".
        /// </summary>
        public event Action OnIndexRebuildRequested;

        /// <summary>
        /// Fired when the user clicks "Clear Index".
        /// </summary>
        public event Action OnIndexClearRequested;

        /// <summary>
        /// Fired when the user confirms "Delete All Conversations".
        /// </summary>
        public event Action OnCachePurgeRequested;

        /// <summary>Raised when the user clicks the "Restart Sidecar" button.</summary>
        public event Action OnSidecarRestartRequested;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Initializes a new <see cref="SettingsPanel"/> with the given theme context.
        /// </summary>
        /// <param name="theme">The shared theme context for colors and font sizes.</param>
        public SettingsPanel(ThemeContext theme)
        {
            _theme = theme;
            style.flexGrow = 1;
        }

        // ── Public Methods ────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the entire settings UI using the provided service references and settings.
        /// Call this whenever the settings view needs to reflect updated state.
        /// </summary>
        /// <param name="settings">The current UniClaude settings instance.</param>
        /// <param name="currentModel">The currently selected model value string.</param>
        /// <param name="currentEffort">The currently selected effort value string.</param>
        /// <param name="projectAwareness">The active project awareness service, or null if disabled.</param>
        /// <param name="sidecar">The active sidecar manager instance.</param>
        /// <param name="mcpServer">The active MCP server instance, or null if stopped.</param>
        public void Refresh(UniClaudeSettings settings, string currentModel, string currentEffort,
            ProjectAwareness projectAwareness, SidecarManager sidecar, MCPServer mcpServer)
        {
            _settings = settings;
            _currentModel = currentModel;
            _currentEffort = currentEffort;
            _projectAwareness = projectAwareness;
            _sidecar = sidecar;
            _mcpServer = mcpServer;

            this.Clear();
            _rebuildBanner = null;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingTop = 16;
            scroll.style.paddingLeft = 16;
            scroll.style.paddingRight = 16;

            // Section: Font Size
            scroll.Add(MakeSectionHeader("Font Size"));

            var fontSizeLabels = new List<string> { "Small", "Medium", "Large", "Extra Large" };
            var fontSizeValues = new List<string> { "small", "medium", "large", "xlarge" };
            var currentFontIndex = fontSizeValues.IndexOf(_settings.ChatFontSize ?? "medium");
            if (currentFontIndex < 0) currentFontIndex = 1;

            var fontSizeDropdown = new PopupField<string>(fontSizeLabels, currentFontIndex);
            fontSizeDropdown.style.marginBottom = 16;
            fontSizeDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = fontSizeLabels.IndexOf(evt.newValue);
                if (idx >= 0)
                {
                    OnFontSizeChanged?.Invoke(fontSizeValues[idx]);
                }
            });
            scroll.Add(fontSizeDropdown);

            scroll.Add(MakeSeparator());

            // Section: Install Mode
            scroll.Add(MakeSectionHeader("Install Mode"));
            scroll.Add(new InstallModeSection());
            scroll.Add(MakeSeparator());

            // Section: Model
            scroll.Add(MakeSectionHeader("Model"));

            var modelLabels = UniClaudeSettings.ModelChoices.Select(m => m.Label).ToList();
            var modelValues = UniClaudeSettings.ModelChoices.Select(m => m.Value).ToList();
            var currentIndex = modelValues.IndexOf(_currentModel ?? "");
            if (currentIndex < 0) currentIndex = 0;

            var modelDropdown = new PopupField<string>(modelLabels, currentIndex);
            modelDropdown.style.marginBottom = 16;
            modelDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = modelLabels.IndexOf(evt.newValue);
                if (idx >= 0)
                {
                    OnModelChanged?.Invoke(modelValues[idx]);
                }
            });
            scroll.Add(modelDropdown);

            scroll.Add(MakeSeparator());

            // Section: Effort
            scroll.Add(MakeSectionHeader("Reasoning Effort"));

            var effortLabels = UniClaudeSettings.EffortChoices.Select(e => e.Label).ToList();
            var effortValues = UniClaudeSettings.EffortChoices.Select(e => e.Value).ToList();
            var currentEffortIndex = effortValues.IndexOf(_currentEffort ?? "");
            if (currentEffortIndex < 0) currentEffortIndex = 1; // default to Medium

            var effortDropdown = new PopupField<string>(effortLabels, currentEffortIndex);
            effortDropdown.style.marginBottom = 16;
            effortDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                var idx = effortLabels.IndexOf(evt.newValue);
                if (idx >= 0)
                {
                    OnEffortChanged?.Invoke(effortValues[idx]);
                }
            });
            scroll.Add(effortDropdown);

            scroll.Add(MakeSeparator());

            // Section: Cache
            scroll.Add(MakeSectionHeader("Cache"));

            var locationLabel = new Label($"Location: {ConversationStore.BaseDir}");
            locationLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            locationLabel.style.marginBottom = 4;
            scroll.Add(locationLabel);

            var (count, bytes) = ConversationStore.GetCacheStats();
            var sizeStr = bytes < 1024 * 1024
                ? $"{bytes / 1024f:F1} KB"
                : $"{bytes / (1024f * 1024f):F1} MB";
            var statsLabel = new Label($"{count} conversations \u00b7 {sizeStr}");
            statsLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            statsLabel.style.color = _theme.IsDark ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.45f, 0.45f, 0.48f);
            statsLabel.style.marginBottom = 12;
            scroll.Add(statsLabel);

            var purgeBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("Delete All Conversations",
                    $"This will permanently delete all {count} saved conversations.", "Delete All", "Cancel"))
                {
                    ConversationStore.DeleteAll();
                    OnCachePurgeRequested?.Invoke();
                }
            }) { text = "Delete All Conversations" };
            purgeBtn.style.height = 28;
            purgeBtn.style.marginBottom = 16;
            if (count == 0) purgeBtn.SetEnabled(false);
            scroll.Add(purgeBtn);

            // Section: Project Awareness
            scroll.Add(MakeSectionHeader("Project Awareness"));

            var awarenessToggle = new Toggle("Enabled") { value = _settings.ProjectAwarenessEnabled };
            awarenessToggle.style.marginBottom = 8;
            awarenessToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                _settings.ProjectAwarenessEnabled = evt.newValue;
                UniClaudeSettings.Save(_settings);
                OnProjectAwarenessToggled?.Invoke();
            });
            scroll.Add(awarenessToggle);

            if (_settings.ProjectAwarenessEnabled)
            {
                // Index stats
                if (_projectAwareness != null)
                {
                    var idx = _projectAwareness.GetIndex();
                    var scriptCount = 0;
                    var prefabCount = 0;
                    var sceneCount = 0;
                    foreach (var e in idx.Entries)
                    {
                        if (e.Kind == AssetKind.Script) scriptCount++;
                        else if (e.Kind == AssetKind.Prefab) prefabCount++;
                        else if (e.Kind == AssetKind.Scene) sceneCount++;
                    }

                    var indexStatsLabel = new Label($"Index: {scriptCount} scripts, {prefabCount} prefabs, {sceneCount} scenes ({idx.Entries.Count} total)");
                    indexStatsLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
                    indexStatsLabel.style.marginBottom = 4;
                    scroll.Add(indexStatsLabel);

                    var (cacheExists, cacheBytes) = ProjectIndexStore.GetCacheStats();
                    if (cacheExists)
                    {
                        var cacheSizeStr = cacheBytes < 1024 * 1024
                            ? $"{cacheBytes / 1024f:F1} KB"
                            : $"{cacheBytes / (1024f * 1024f):F1} MB";
                        var cacheLabel = new Label($"Cache: {cacheSizeStr}");
                        cacheLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
                        cacheLabel.style.color = _theme.IsDark ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.45f, 0.45f, 0.48f);
                        cacheLabel.style.marginBottom = 12;
                        scroll.Add(cacheLabel);
                    }

                    if (!string.IsNullOrEmpty(idx.LastFullScan))
                    {
                        var lastScanLabel = new Label($"Last full scan: {HistoryPanel.FormatRelativeTime(idx.LastFullScan)}");
                        lastScanLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
                        lastScanLabel.style.color = _theme.IsDark ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.45f, 0.45f, 0.48f);
                        lastScanLabel.style.marginBottom = 12;
                        scroll.Add(lastScanLabel);
                    }
                }

                var rebuildBtn = new Button(() =>
                {
                    OnIndexRebuildRequested?.Invoke();
                }) { text = "Full Index Rebuild" };
                rebuildBtn.style.height = 28;
                rebuildBtn.style.marginBottom = 4;
                scroll.Add(rebuildBtn);

                var clearBtn = new Button(() =>
                {
                    OnIndexClearRequested?.Invoke();
                }) { text = "Clear Index" };
                clearBtn.style.height = 28;
                clearBtn.style.marginBottom = 16;
                scroll.Add(clearBtn);
            }

            // Section: Package Indexing
            if (_settings.ProjectAwarenessEnabled && _projectAwareness != null)
            {
                scroll.Add(MakeSeparator());

                var pkgFoldout = MakeSectionFoldout("Package Indexing", _pkgFoldoutExpanded);
                pkgFoldout.RegisterValueChangedCallback(evt => _pkgFoldoutExpanded = evt.newValue);
                scroll.Add(pkgFoldout);

                var packages = _projectAwareness.GetDiscoveredPackages();
                if (packages.Count == 0)
                {
                    var noPackagesLabel = new Label("No packages discovered.");
                    noPackagesLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                    noPackagesLabel.style.marginBottom = 8;
                    pkgFoldout.Add(noPackagesLabel);
                }
                else
                {
                    var localPackages = packages.Where(p => p.IsLocal).OrderBy(p => p.Name).ToList();
                    var registryPackages = packages.Where(p => !p.IsLocal).OrderBy(p => p.Name).ToList();

                    if (localPackages.Count > 0)
                    {
                        var localFoldout = new Foldout { text = $"Local ({localPackages.Count})", value = _localPkgFoldoutExpanded };
                        localFoldout.RegisterValueChangedCallback(evt => _localPkgFoldoutExpanded = evt.newValue);
                        localFoldout.style.marginBottom = 4;
                        pkgFoldout.Add(localFoldout);

                        foreach (var pkg in localPackages)
                            localFoldout.Add(MakePackageRow(pkg, scroll));
                    }

                    if (registryPackages.Count > 0)
                    {
                        var registryFoldout = new Foldout { text = $"Registry ({registryPackages.Count})", value = _registryPkgFoldoutExpanded };
                        registryFoldout.RegisterValueChangedCallback(evt => _registryPkgFoldoutExpanded = evt.newValue);
                        registryFoldout.style.marginBottom = 4;
                        pkgFoldout.Add(registryFoldout);

                        foreach (var pkg in registryPackages)
                            registryFoldout.Add(MakePackageRow(pkg, scroll));
                    }
                }

                // Section: Project Folders
                scroll.Add(MakeSeparator());

                var folderFoldout = MakeSectionFoldout("Project Folders", _folderFoldoutExpanded);
                folderFoldout.RegisterValueChangedCallback(evt => _folderFoldoutExpanded = evt.newValue);
                scroll.Add(folderFoldout);

                var assetsDir = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets");
                if (Directory.Exists(assetsDir))
                {
                    BuildFolderTree(folderFoldout, scroll, assetsDir, "Assets", 0);
                }
                else
                {
                    var noAssetsLabel = new Label("Assets folder not found.");
                    noAssetsLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                    folderFoldout.Add(noAssetsLabel);
                }
            }

            scroll.Add(MakeSeparator());

            // Section: MCP Server
            scroll.Add(MakeSectionHeader("MCP Server"));

            var mcpSettings = new MCPSettings();

            var mcpToggle = new Toggle("Enabled") { value = mcpSettings.Enabled };
            mcpToggle.style.marginBottom = 8;
            mcpToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                mcpSettings.Enabled = evt.newValue;
                OnSettingsChanged?.Invoke();
            });
            scroll.Add(mcpToggle);

            var mcpStatusText = MCPServer.Instance != null && MCPServer.Instance.IsRunning
                ? $"Running on {MCPServer.Instance.Endpoint}"
                : "Stopped";
            var mcpStatusLabel = new Label($"Status: {mcpStatusText}");
            mcpStatusLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            mcpStatusLabel.style.marginBottom = 8;
            mcpStatusLabel.style.color = MCPServer.Instance != null && MCPServer.Instance.IsRunning
                ? new Color(0.4f, 0.8f, 0.4f)
                : (_theme.IsDark ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.45f, 0.45f, 0.48f));
            scroll.Add(mcpStatusLabel);

            if (mcpSettings.Enabled)
            {
                var portField = new IntegerField("Port (0 = auto)") { value = mcpSettings.Port };
                portField.style.marginBottom = 8;
                portField.RegisterCallback<ChangeEvent<int>>(evt =>
                {
                    mcpSettings.Port = evt.newValue;
                });
                scroll.Add(portField);

                var strategyLabels = new List<string> { "Auto", "Manual" };
                var strategyIndex = (int)mcpSettings.DomainReloadStrategy;
                var strategyDropdown = new PopupField<string>("Domain Reload", strategyLabels, strategyIndex);
                strategyDropdown.style.marginBottom = 4;
                strategyDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    mcpSettings.DomainReloadStrategy = evt.newValue == "Manual"
                        ? ReloadStrategy.Manual
                        : ReloadStrategy.Auto;
                });
                scroll.Add(strategyDropdown);

                var strategyHint = new Label(
                    mcpSettings.DomainReloadStrategy == ReloadStrategy.Auto
                        ? "Auto: locks on first tool call, unlocks when turn ends."
                        : "Manual: use BeginScriptEditing/EndScriptEditing tools to control.");
                strategyHint.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
                strategyHint.style.color = _theme.IsDark ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);
                strategyHint.style.marginBottom = 12;
                scroll.Add(strategyHint);

                var verboseToggle = new Toggle("Verbose tool logging") { value = mcpSettings.VerboseToolLogging };
                verboseToggle.style.marginBottom = 16;
                verboseToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
                {
                    mcpSettings.VerboseToolLogging = evt.newValue;
                });
                scroll.Add(verboseToggle);
            }

            scroll.Add(MakeSeparator());

            // Section: Sidecar
            scroll.Add(MakeSectionHeader("Agent Sidecar"));

            var sidecarStatus = _sidecar != null && _sidecar.IsRunning
                ? $"Running on port {_sidecar.Port}"
                : "Stopped";
            var sidecarStatusLabel = new Label($"Status: {sidecarStatus}");
            sidecarStatusLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            sidecarStatusLabel.style.marginBottom = 8;
            sidecarStatusLabel.style.color = _sidecar != null && _sidecar.IsRunning
                ? new Color(0.4f, 0.8f, 0.4f)
                : (_theme.IsDark ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.45f, 0.45f, 0.48f));
            scroll.Add(sidecarStatusLabel);

            var restartBtn = new Button(() => OnSidecarRestartRequested?.Invoke()) { text = "Restart Sidecar" };
            restartBtn.style.marginBottom = 12;
            restartBtn.style.alignSelf = Align.FlexStart;
            scroll.Add(restartBtn);

            var nodePathField = new TextField("Node.js Path (empty = auto)") { value = _settings.NodePath };
            nodePathField.style.marginBottom = 12;
            nodePathField.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                _settings.NodePath = evt.newValue;
                UniClaudeSettings.Save(_settings);
                OnSettingsChanged?.Invoke();
            });
            scroll.Add(nodePathField);

            var sidecarVerboseToggle = new Toggle("Verbose logging") { value = _settings.VerboseLogging };
            sidecarVerboseToggle.style.marginBottom = 12;
            sidecarVerboseToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                _settings.VerboseLogging = evt.newValue;
                UniClaudeSettings.Save(_settings);
                OnSettingsChanged?.Invoke();
            });
            scroll.Add(sidecarVerboseToggle);

            var sidecarVerboseHint = new Label("When off, only errors from the sidecar appear in the Console.");
            sidecarVerboseHint.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            sidecarVerboseHint.style.color = _theme.IsDark ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);
            sidecarVerboseHint.style.marginTop = -8;
            sidecarVerboseHint.style.marginBottom = 12;
            scroll.Add(sidecarVerboseHint);

            // Context token budget
            var budgetField = new IntegerField("Context token budget") { value = _settings.ContextTokenBudget };
            budgetField.style.marginBottom = 4;
            budgetField.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                _settings.ContextTokenBudget = Math.Max(0, evt.newValue);
                UniClaudeSettings.Save(_settings);
                OnSettingsChanged?.Invoke();
            });
            scroll.Add(budgetField);

            var budgetHint = new Label(
                "Max tokens for the project tree sent with every message. " +
                "Lower values reduce cost but give Claude less project visibility. " +
                "0 = unlimited (full tree \u2014 may be expensive on large projects). " +
                "Default: 3300 (~$0.01/message at Sonnet pricing).");
            budgetHint.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            budgetHint.style.color = _theme.IsDark ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);
            budgetHint.style.whiteSpace = WhiteSpace.Normal;
            budgetHint.style.marginBottom = 12;
            budgetHint.style.marginLeft = 4;
            scroll.Add(budgetHint);

            // Auto-allow MCP tools toggle with tooltip
            var mcpAutoRow = new VisualElement();
            mcpAutoRow.style.flexDirection = FlexDirection.Row;
            mcpAutoRow.style.alignItems = Align.Center;
            mcpAutoRow.style.marginBottom = 4;

            var mcpAutoToggle = new Toggle("Auto-approve MCP tools") { value = _settings.AutoAllowMCPTools };
            mcpAutoToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                _settings.AutoAllowMCPTools = evt.newValue;
                UniClaudeSettings.Save(_settings);
                OnSettingsChanged?.Invoke();
            });
            mcpAutoRow.Add(mcpAutoToggle);

            var mcpInfoLabel = new Label("\u24D8");
            mcpInfoLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            mcpInfoLabel.style.marginLeft = 4;
            mcpInfoLabel.style.color = _theme.IsDark ? new Color(0.5f, 0.5f, 0.6f) : new Color(0.4f, 0.4f, 0.5f);
            mcpInfoLabel.style.cursor = new UnityEngine.UIElements.Cursor();
            mcpInfoLabel.tooltip =
                "When enabled, UniClaude MCP tools run without asking.\n\n" +
                "FILE TOOLS\n" +
                "  file_read — Read file contents\n" +
                "  file_write — Write or overwrite a file\n" +
                "  file_create_script — Create a C# script from template\n" +
                "  file_modify_script — Find and replace in a script\n" +
                "  file_delete — Delete a file and its .meta\n" +
                "  file_find — Search files by glob pattern\n\n" +
                "SCENE TOOLS\n" +
                "  scene_get_hierarchy — List all GameObjects\n" +
                "  scene_create_gameobject — Create a GameObject\n" +
                "  scene_delete_gameobject — Delete a GameObject\n" +
                "  scene_reparent_gameobject — Move under new parent\n" +
                "  scene_rename_gameobject — Rename a GameObject\n" +
                "  scene_setup — Batch-create GameObjects with components\n\n" +
                "ASSET TOOLS\n" +
                "  asset_get_info — Asset metadata and dependencies\n" +
                "  asset_find — Search assets by filter\n" +
                "  asset_move — Move or rename an asset\n" +
                "  asset_import — Force reimport an asset\n\n" +
                "PREFAB TOOLS\n" +
                "  prefab_create — Save GameObject as prefab\n" +
                "  prefab_instantiate — Instantiate prefab in scene\n" +
                "  prefab_apply_overrides — Apply overrides to source\n" +
                "  prefab_get_contents — Inspect prefab hierarchy\n\n" +
                "COMPONENT TOOLS\n" +
                "  component_add — Add a component\n" +
                "  component_find — Find objects with component\n" +
                "  component_remove — Remove a component\n" +
                "  component_get_all — List all components\n" +
                "  component_get_property — Read a property value\n" +
                "  component_set_property — Set a property value\n" +
                "  component_set_properties — Batch set properties\n\n" +
                "PROJECT TOOLS\n" +
                "  project_run_tests — Run unit tests\n" +
                "  project_get_console_log — Get console entries\n" +
                "  project_get_settings — Read project settings\n" +
                "  project_refresh_assets — Refresh AssetDatabase\n" +
                "  project_search — Search project files\n\n" +
                "INSPECTOR TOOLS\n" +
                "  inspector_select — Select object in Editor\n" +
                "  inspector_inspect — Full property dump\n\n" +
                "SCRIPT EDITING\n" +
                "  BeginScriptEditing — Lock domain reload\n" +
                "  EndScriptEditing — Unlock and compile";
            mcpAutoRow.Add(mcpInfoLabel);
            scroll.Add(mcpAutoRow);

            var mcpAutoHint = new Label("Skip permission prompts for UniClaude's built-in Unity tools.");
            mcpAutoHint.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            mcpAutoHint.style.color = _theme.IsDark ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);
            mcpAutoHint.style.marginTop = -4;
            mcpAutoHint.style.marginBottom = 12;
            scroll.Add(mcpAutoHint);

            // Section: Session Trust
            scroll.Add(MakeSectionHeader("Session Trust"));

            var trustHint = new Label("Tools approved with 'Always Allow' during this session:");
            trustHint.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            trustHint.style.color = _theme.IsDark ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.5f, 0.5f, 0.55f);
            trustHint.style.marginBottom = 4;
            scroll.Add(trustHint);

            // Trust list will be populated from /health response
            var trustList = new Label("(none)");
            trustList.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            trustList.style.marginBottom = 8;
            trustList.style.color = _theme.IsDark ? new Color(0.6f, 0.6f, 0.65f) : new Color(0.4f, 0.4f, 0.45f);
            scroll.Add(trustList);

            // Async fetch trust list from sidecar
            if (_sidecar != null && _sidecar.IsRunning)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var http = new System.Net.Http.HttpClient();
                        var resp = await http.GetStringAsync($"http://127.0.0.1:{_sidecar.Port}/health");
                        var json = Newtonsoft.Json.Linq.JObject.Parse(resp);
                        var tools = json["trusted_tools"]?.ToObject<string[]>() ?? System.Array.Empty<string>();
                        EditorApplication.delayCall += () =>
                        {
                            trustList.text = tools.Length > 0
                                ? string.Join(", ", tools)
                                : "(none)";
                        };
                    }
                    catch { /* Sidecar may not be ready yet */ }
                });
            }

            scroll.Add(MakeSeparator());

            // Section: About
            scroll.Add(MakeSectionHeader("About"));

            var nodePath = SidecarManager.FindNodeBinary(_settings.NodePath) ?? "not found";
            var nodeLabel = new Label($"Node.js: {nodePath}");
            nodeLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            nodeLabel.style.marginBottom = 4;
            scroll.Add(nodeLabel);

            var entryPoint = SidecarManager.GetSidecarEntryPoint();
            var sidecarPathLabel = new Label($"Sidecar: {entryPoint}");
            sidecarPathLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            sidecarPathLabel.style.color = _theme.IsDark ? new Color(0.55f, 0.55f, 0.58f) : new Color(0.45f, 0.45f, 0.48f);
            scroll.Add(sidecarPathLabel);

            this.Add(scroll);
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Shows a yellow-tinted rebuild banner inside the given parent element,
        /// informing the user that index filters have changed and a rebuild is needed.
        /// Does nothing if the banner is already visible.
        /// </summary>
        /// <param name="parent">The container element to insert the banner into.</param>
        void ShowRebuildBanner(VisualElement parent)
        {
            if (_rebuildBanner != null) return; // Already showing

            _rebuildBanner = new VisualElement();
            _rebuildBanner.style.flexDirection = FlexDirection.Row;
            _rebuildBanner.style.alignItems = Align.Center;
            _rebuildBanner.style.backgroundColor = _theme.IsDark ? new Color(0.25f, 0.20f, 0.10f) : new Color(1f, 0.96f, 0.85f);
            _rebuildBanner.style.borderTopLeftRadius = 4;
            _rebuildBanner.style.borderTopRightRadius = 4;
            _rebuildBanner.style.borderBottomLeftRadius = 4;
            _rebuildBanner.style.borderBottomRightRadius = 4;
            _rebuildBanner.style.paddingTop = 8;
            _rebuildBanner.style.paddingBottom = 8;
            _rebuildBanner.style.paddingLeft = 12;
            _rebuildBanner.style.paddingRight = 12;
            _rebuildBanner.style.marginTop = 12;
            _rebuildBanner.style.marginBottom = 8;

            var bannerLabel = new Label("Index filters changed \u2014 Rebuild to apply");
            bannerLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            bannerLabel.style.flexGrow = 1;
            _rebuildBanner.Add(bannerLabel);

            var rebuildBtn = new Button(() =>
            {
                if (_projectAwareness != null)
                {
                    OnIndexRebuildRequested?.Invoke();
                    _rebuildBanner = null;
                }
            }) { text = "Rebuild" };
            rebuildBtn.style.height = 24;
            _rebuildBanner.Add(rebuildBtn);

            parent.Add(_rebuildBanner);
        }

        /// <summary>
        /// Creates a bold section header label styled with the Header font tier.
        /// </summary>
        /// <param name="text">The header text to display.</param>
        /// <returns>A styled <see cref="Label"/> element.</returns>
        Label MakeSectionHeader(string text)
        {
            var label = new Label(text);
            label.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Header);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 8;
            return label;
        }

        /// <summary>
        /// Creates a collapsible section foldout styled with the Header font tier.
        /// </summary>
        /// <param name="text">The foldout header text.</param>
        /// <param name="startExpanded">Whether the foldout starts in the expanded state.</param>
        /// <returns>A styled <see cref="Foldout"/> element.</returns>
        Foldout MakeSectionFoldout(string text, bool startExpanded = false)
        {
            var foldout = new Foldout { text = text, value = startExpanded };
            foldout.style.marginTop = 4;
            foldout.style.marginBottom = 4;
            var toggle = foldout.Q<Toggle>();
            if (toggle != null)
            {
                var label = toggle.Q<Label>();
                if (label != null)
                {
                    label.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Header);
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
            }
            return foldout;
        }

        /// <summary>
        /// Creates a toggle row for a single package, wired to show the rebuild banner
        /// when the user changes the inclusion state.
        /// </summary>
        /// <param name="pkg">The package metadata to render.</param>
        /// <param name="bannerParent">The element to insert the rebuild banner into when triggered.</param>
        /// <returns>A <see cref="VisualElement"/> row containing the toggle and package name.</returns>
        VisualElement MakePackageRow(PackageInfo pkg, VisualElement bannerParent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var isIncluded = IndexFilterSettings.IsPackageIncluded(pkg, _settings);
            var toggle = new Toggle() { value = isIncluded };
            toggle.style.marginRight = 8;

            var pkgName = pkg.Name;
            toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                _settings.PackageIndexOverrides[pkgName] = evt.newValue;
                UniClaudeSettings.Save(_settings);
                ShowRebuildBanner(bannerParent);
            });
            row.Add(toggle);

            var versionStr = string.IsNullOrEmpty(pkg.Version) ? "" : $" ({pkg.Version})";
            var nameLabel = new Label($"{pkg.DisplayName ?? pkg.Name}{versionStr}");
            nameLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            nameLabel.style.flexGrow = 1;
            if (!pkg.IsLocal)
                nameLabel.style.opacity = 0.6f;
            row.Add(nameLabel);

            return row;
        }

        /// <summary>
        /// Creates a thin horizontal separator line for visual section division.
        /// </summary>
        /// <returns>A <see cref="VisualElement"/> styled as a horizontal rule.</returns>
        VisualElement MakeSeparator()
        {
            var sep = new VisualElement();
            sep.style.height = 1;
            sep.style.backgroundColor = _theme.IsDark ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.8f, 0.8f, 0.8f);
            sep.style.marginTop = 8;
            sep.style.marginBottom = 8;
            return sep;
        }

        /// <summary>
        /// Recursively builds a folder tree UI up to a maximum depth of 3 levels,
        /// with per-folder include/exclude toggles wired to <see cref="_settings"/>.ExcludedFolders.
        /// </summary>
        /// <param name="parent">The parent container to add folder rows to.</param>
        /// <param name="bannerParent">The element to insert the rebuild banner into when a folder toggle changes.</param>
        /// <param name="fullPath">The absolute filesystem path of the directory to enumerate.</param>
        /// <param name="relativePath">The project-relative path of the directory (e.g. "Assets/Foo").</param>
        /// <param name="depth">The current recursion depth, starting at 0 for Assets/.</param>
        void BuildFolderTree(VisualElement parent, VisualElement bannerParent, string fullPath, string relativePath, int depth)
        {
            const int maxDepth = 2; // 3 levels: Assets/, Assets/Foo/, Assets/Foo/Bar/

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(fullPath);
            }
            catch
            {
                return; // Skip inaccessible directories
            }

            if (subdirs.Length == 0) return;

            System.Array.Sort(subdirs);

            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                var childRelative = relativePath + "/" + dirName;
                var isExcluded = _settings.ExcludedFolders.Contains(childRelative);
                var hasChildDirs = false;
                try { hasChildDirs = depth < maxDepth && Directory.GetDirectories(subdir).Length > 0; }
                catch { }
                var hasChildren = hasChildDirs && !isExcluded;

                var capturedPath = childRelative;

                // Header row: [checkbox] [▶/▼ arrow] [folder name]
                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.marginBottom = 2;

                var toggle = new Toggle() { value = !isExcluded };
                toggle.style.marginRight = 4;
                header.Add(toggle);

                VisualElement childrenContainer = null;
                Label arrow = null;

                if (hasChildren)
                {
                    arrow = new Label("\u25b6");
                    arrow.style.fontSize = 10;
                    arrow.style.width = 14;
                    arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                    arrow.style.cursor = new UnityEngine.UIElements.Cursor();
                    header.Add(arrow);

                    childrenContainer = new VisualElement();
                    childrenContainer.style.display = DisplayStyle.None;
                    childrenContainer.style.paddingLeft = 18;
                }

                var nameLabel = new Label(dirName + "/");
                nameLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                if (isExcluded)
                    nameLabel.style.opacity = 0.4f;
                header.Add(nameLabel);

                // Wire up checkbox
                toggle.RegisterCallback<ChangeEvent<bool>>(evt =>
                {
                    if (evt.newValue)
                        _settings.ExcludedFolders.Remove(capturedPath);
                    else if (!_settings.ExcludedFolders.Contains(capturedPath))
                        _settings.ExcludedFolders.Add(capturedPath);

                    UniClaudeSettings.Save(_settings);
                    ShowRebuildBanner(bannerParent);
                    if (hasChildDirs)
                        OnSettingsChanged?.Invoke();
                });

                // Wire up expand/collapse on arrow and label click
                if (hasChildren && arrow != null && childrenContainer != null)
                {
                    var arrowRef = arrow;
                    var containerRef = childrenContainer;
                    var expanded = false;

                    System.Action toggleExpand = () =>
                    {
                        expanded = !expanded;
                        containerRef.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                        arrowRef.text = expanded ? "\u25bc" : "\u25b6";
                    };

                    arrow.RegisterCallback<ClickEvent>(evt => toggleExpand());
                    nameLabel.RegisterCallback<ClickEvent>(evt => toggleExpand());
                }

                parent.Add(header);

                if (hasChildren && childrenContainer != null)
                {
                    parent.Add(childrenContainer);
                    BuildFolderTree(childrenContainer, bannerParent, subdir, childRelative, depth + 1);
                }
            }
        }
    }
}
