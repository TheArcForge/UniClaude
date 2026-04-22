using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniClaude.Editor.MCP;
using UniClaude.Editor.UI;
using UniClaude.Editor.UI.Input;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor
{
    /// <summary>
    /// Serializable attachment data sent to the sidecar's /chat endpoint.
    /// </summary>
    public class SidecarAttachment
    {
        /// <summary>Content type: "text" for file content or "image" for base64-encoded image data.</summary>
        public string Type;

        /// <summary>Original file name including extension.</summary>
        public string FileName;

        /// <summary>The payload: plain text for file attachments, base64-encoded data for images.</summary>
        public string Content;

        /// <summary>MIME type (e.g. "image/png"). Only set when <see cref="Type"/> is "image".</summary>
        public string MediaType;
    }

    /// <summary>
    /// Main UniClaude editor window. Thin orchestrator shell that owns services and
    /// wires together tab-based panels (Chat, History, Settings).
    /// </summary>
    public class UniClaudeWindow : EditorWindow
    {
        // ── Services ──
        SidecarManager _sidecar;
        SidecarClient _client;
        SlashCommandRegistry _commands;
        UniClaudeSettings _settings;
        ProjectAwareness _projectAwareness;
        MCPServer _mcpServer;

        // ── State ──
        Conversation _conversation;
        string _currentModel;
        string _currentEffort;
        bool _isGenerating;
        bool _planMode;
        ActivityLog _currentActivity;
        double _lastSendTime;
        HealthCheckRunner _healthCheckRunner;

        // ── Reconnection ──
        const int MaxReconnectAttempts = 3;
        int _reconnectAttempt;

        // ── Session State Keys ──
        const string SessionKey_IsGenerating = "UniClaude_IsGenerating";
        const string SessionKey_LastEventId = "UniClaude_LastEventId";
        const string SessionKey_StreamingContent = "UniClaude_StreamingContent";

        // ── Theme ──
        ThemeContext _theme;

        // ── UI Components ──
        VisualElement _setupContainer;
        VisualElement _mainContainer;
        ChatPanel _chatPanel;
        InputController _inputController;
        HistoryPanel _historyPanel;
        SettingsPanel _settingsPanel;
        Label _toolbarTitle;
        Label _toolbarStatus;
        Button _tabChat, _tabHistory, _tabSettings;

        enum Tab { Chat, History, Settings }
        Tab _activeTab = Tab.Chat;

        enum SetupState { NodeMissing, DepsNeeded, Verifying, AuthMissing }

        // ── Window Entry Point ──

        /// <summary>
        /// Opens the UniClaude window from the ArcForge menu.
        /// </summary>
        [MenuItem("ArcForge/UniClaude")]
        public static void ShowWindow()
        {
            var window = GetWindow<UniClaudeWindow>("UniClaude");
            window.minSize = new Vector2(400, 300);
        }

        // ── Unity Lifecycle ──

        void OnEnable()
        {
            _sidecar = new SidecarManager();
            _settings = UniClaudeSettings.Load();
            _currentModel = _settings.SelectedModel;
            _currentEffort = _settings.SelectedEffort;

            _theme = new ThemeContext { FontPreset = _settings.ChatFontSize ?? "medium" };

            var resumeId = SessionState.GetString("UniClaude_ConversationId", null);
            _conversation = resumeId != null ? ConversationStore.Load(resumeId) : null;
            _conversation ??= new Conversation();

            _commands = new SlashCommandRegistry();
            RegisterLocalCommands();
            _commands.DiscoverCliCommands();

            if (_settings.ProjectAwarenessEnabled)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    _projectAwareness = new ProjectAwareness();
                    _projectAwareness.Initialize(
                        System.IO.Path.GetDirectoryName(Application.dataPath));
                };
            }

            // MCP server — respect MCPSettings.Enabled and reuse existing instance
            var mcpSettings = new MCPSettings();
            if (mcpSettings.Enabled && (MCPServer.Instance == null || !MCPServer.Instance.IsRunning))
            {
                _mcpServer = new MCPServer();
                _mcpServer.Start(mcpSettings);
            }
            else if (MCPServer.Instance != null)
            {
                _mcpServer = MCPServer.Instance;
            }

            // Subscribe to MCP events (needed on both fresh-create and reuse paths)
            if (_mcpServer != null)
            {
                _mcpServer.OnToolExecuted += OnMCPToolExecuted;
                if (_mcpServer.ActiveReloadStrategy != null)
                    _mcpServer.ActiveReloadStrategy.OnLog += OnMCPLog;
            }

            EditorApplication.update += _sidecar.HealthPing;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;

            RebuildUI();

            // Deferred sidecar start to avoid blocking OnEnable
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                var nodePath = SidecarManager.FindNodeBinary(_settings.NodePath);
                if (nodePath == null)
                    ShowSetupPanel(SetupState.NodeMissing);
                else if (!SidecarManager.IsSetupComplete)
                    ShowSetupPanel(SetupState.DepsNeeded);
                else
                    ShowSetupPanel(SetupState.Verifying);
            };
        }

        void OnDisable()
        {
            SessionState.SetString("UniClaude_ConversationId", _conversation.Id);
            SaveCurrentConversation();
            _inputController?.Clear();
            _projectAwareness?.Dispose();
            _projectAwareness = null;

            EditorApplication.update -= _sidecar.HealthPing;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveStateBeforeReload;
            DisconnectClient();
            _sidecar?.Dispose();

            if (_mcpServer != null)
            {
                _mcpServer.OnToolExecuted -= OnMCPToolExecuted;
                if (_mcpServer.ActiveReloadStrategy != null)
                    _mcpServer.ActiveReloadStrategy.OnLog -= OnMCPLog;
            }
            _mcpServer?.Dispose();
            _mcpServer = null;
        }

        /// <summary>
        /// UI Toolkit hook. Intentionally empty — OnEnable drives tree building via
        /// RebuildUI() so domain reloads (which re-run OnEnable but not CreateGUI)
        /// rebuild the tree instead of leaving it orphaned with null C# references.
        /// </summary>
        public void CreateGUI()
        {
        }

        /// <summary>
        /// Clears rootVisualElement and rebuilds the full UI tree. Called from OnEnable
        /// so fresh-open and domain-reload both land on a clean, consistent state.
        /// </summary>
        void RebuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;

            BuildLayout(root);

            if (_conversation.Messages.Count > 0)
                _chatPanel.RebuildMessages(_conversation);
        }

        // ── Layout ──

        void BuildLayout(VisualElement root)
        {
            _mainContainer = new VisualElement();
            _mainContainer.style.flexGrow = 1;
            _mainContainer.style.flexDirection = FlexDirection.Column;

            // Toolbar with tabs
            var toolbar = new Toolbar();
            _tabChat = MakeTabButton("Chat", Tab.Chat);
            _tabHistory = MakeTabButton("History", Tab.History);
            _tabSettings = MakeTabButton("Settings", Tab.Settings);
            toolbar.Add(_tabChat);
            toolbar.Add(_tabHistory);
            toolbar.Add(_tabSettings);

            _toolbarTitle = new Label($"UniClaude \u2014 {_conversation.Title}");
            _toolbarTitle.style.flexGrow = 1;
            _toolbarTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            _toolbarTitle.style.paddingLeft = 8;
            _toolbarTitle.style.overflow = Overflow.Hidden;
            _toolbarTitle.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            toolbar.Add(_toolbarTitle);

            _toolbarStatus = new Label("Thinking...");
            _toolbarStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
            _toolbarStatus.style.width = 70;
            _toolbarStatus.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            _toolbarStatus.style.display = DisplayStyle.None;
            toolbar.Add(_toolbarStatus);

            _mainContainer.Add(toolbar);

            // View container — holds all three tab panels
            var viewContainer = new VisualElement();
            viewContainer.style.flexGrow = 1;

            _chatPanel = new ChatPanel(_theme);
            _chatPanel.OnAbortRequested += () => OnGenerationComplete();
            _chatPanel.OnStarterClicked += text => HandleInputSubmit(new MessageSubmission(text, new List<AttachmentInfo>()));
            viewContainer.Add(_chatPanel);

            _historyPanel = new HistoryPanel(_theme);
            _historyPanel.style.display = DisplayStyle.None;
            _historyPanel.OnConversationSelected += LoadConversation;
            _historyPanel.OnNewChat += StartNewChat;
            _historyPanel.OnClearAll += ClearAllConversations;
            viewContainer.Add(_historyPanel);

            _settingsPanel = new SettingsPanel(_theme);
            _settingsPanel.style.display = DisplayStyle.None;
            _settingsPanel.OnFontSizeChanged += HandleFontSizeChanged;
            _settingsPanel.OnModelChanged += HandleModelChanged;
            _settingsPanel.OnEffortChanged += HandleEffortChanged;
            _settingsPanel.OnSettingsChanged += () => { /* refresh if needed */ };
            _settingsPanel.OnProjectAwarenessToggled += HandleProjectAwarenessToggled;
            _settingsPanel.OnIndexRebuildRequested += HandleIndexRebuildRequested;
            _settingsPanel.OnIndexClearRequested += HandleIndexClearRequested;
            _settingsPanel.OnCachePurgeRequested += HandleCachePurgeRequested;
            _settingsPanel.OnSidecarRestartRequested += HandleSidecarRestart;
            viewContainer.Add(_settingsPanel);

            _mainContainer.Add(viewContainer);

            // Input controller — always below the view, shown only on Chat tab
            _inputController = new InputController(_theme, _commands);
            _inputController.OnSubmit += HandleInputSubmit;
            _inputController.OnCancelRequested += HandleCancelRequested;
            _mainContainer.Add(_inputController);

            root.Add(_mainContainer);
            UpdateTabStyles();
            RefreshHintText();
        }

        // ── Tab Switching ──

        Button MakeTabButton(string label, Tab tab)
        {
            var btn = new Button(() => SwitchTab(tab)) { text = label };
            btn.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Tab);
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 12;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            return btn;
        }

        void SwitchTab(Tab tab)
        {
            _activeTab = tab;
            _chatPanel.style.display = tab == Tab.Chat ? DisplayStyle.Flex : DisplayStyle.None;
            _historyPanel.style.display = tab == Tab.History ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsPanel.style.display = tab == Tab.Settings ? DisplayStyle.Flex : DisplayStyle.None;
            _inputController.style.display = tab == Tab.Chat ? DisplayStyle.Flex : DisplayStyle.None;

            if (tab == Tab.History) _historyPanel.Refresh(_conversation.Id);
            if (tab == Tab.Settings)
                _settingsPanel.Refresh(_settings, _currentModel, _currentEffort,
                    _projectAwareness, _sidecar, _mcpServer);

            UpdateTabStyles();
        }

        void UpdateTabStyles()
        {
            StyleTab(_tabChat, _activeTab == Tab.Chat);
            StyleTab(_tabHistory, _activeTab == Tab.History);
            StyleTab(_tabSettings, _activeTab == Tab.Settings);
        }

        void StyleTab(Button btn, bool active)
        {
            btn.style.backgroundColor = active ? _theme.TabActive : Color.clear;
            btn.style.color = active ? Color.white
                : _theme.IsDark ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 0;
            btn.style.borderBottomRightRadius = 0;
        }

        // ── Setup Panel ──

        /// <summary>
        /// Displays a first-run setup panel when Node.js or sidecar dependencies are missing.
        /// </summary>
        void ShowSetupPanel(SetupState state)
        {
            if (_setupContainer != null)
            {
                _setupContainer.RemoveFromHierarchy();
                _setupContainer = null;
            }

            if (_mainContainer != null)
                _mainContainer.style.display = DisplayStyle.None;

            _setupContainer = new VisualElement();
            _setupContainer.style.flexGrow = 1;
            _setupContainer.style.justifyContent = Justify.Center;
            _setupContainer.style.alignItems = Align.Center;

            var card = new VisualElement();
            card.style.maxWidth = 500;
            card.style.paddingTop = 24;
            card.style.paddingBottom = 24;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;
            card.style.backgroundColor = _theme.IsDark
                ? new Color(0.18f, 0.18f, 0.22f)
                : new Color(0.94f, 0.94f, 0.96f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;

            var title = new Label("UniClaude Setup");
            title.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            card.Add(title);

            if (state == SetupState.NodeMissing)
            {
                var helpBox = new HelpBox(
                    "Node.js is required but was not found on your system.\n\n" +
                    "Install it from nodejs.org or with a version manager:\n" +
                    "  brew install node\n" +
                    "  — or —\n" +
                    "  https://nodejs.org/\n\n" +
                    "After installing, click Check Again.",
                    HelpBoxMessageType.Warning);
                helpBox.style.marginBottom = 12;
                card.Add(helpBox);

                var checkBtn = new Button(() =>
                {
                    var nodePath = SidecarManager.FindNodeBinary(_settings.NodePath);
                    if (nodePath != null)
                    {
                        _setupContainer.RemoveFromHierarchy();
                        _setupContainer = null;
                        if (!SidecarManager.IsSetupComplete)
                            ShowSetupPanel(SetupState.DepsNeeded);
                        else
                        {
                            ShowSetupPanel(SetupState.Verifying);
                        }
                    }
                }) { text = "Check Again" };
                checkBtn.style.height = 30;
                checkBtn.style.marginBottom = 4;
                card.Add(checkBtn);
            }
            else if (state == SetupState.DepsNeeded)
            {
                var nodePath = SidecarManager.FindNodeBinary(_settings.NodePath);

                var desc = new Label(
                    "UniClaude needs to install its AI engine dependencies.\nThis will:\n" +
                    "  \u2022 Install Node.js packages (~15 seconds)");
                desc.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                desc.style.whiteSpace = WhiteSpace.PreWrap;
                desc.style.marginBottom = 8;
                card.Add(desc);

                var nodeStatus = new Label($"Node.js: \u2713 {nodePath}");
                nodeStatus.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
                nodeStatus.style.color = new Color(0.4f, 0.8f, 0.4f);
                nodeStatus.style.marginBottom = 12;
                card.Add(nodeStatus);

                var progressLabel = new Label();
                progressLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
                progressLabel.style.color = _theme.IsDark
                    ? new Color(0.6f, 0.6f, 0.65f)
                    : new Color(0.4f, 0.4f, 0.45f);
                progressLabel.style.marginBottom = 8;
                progressLabel.style.display = DisplayStyle.None;
                card.Add(progressLabel);

                var setupBtn = new Button { text = "Setup Now" };
                setupBtn.style.height = 30;

                setupBtn.clicked += async () =>
                {
                    setupBtn.SetEnabled(false);
                    progressLabel.style.display = DisplayStyle.Flex;

                    var error = await SidecarManager.RunSetup(nodePath, msg =>
                    {
                        EditorApplication.delayCall += () => progressLabel.text = msg;
                    });

                    if (error != null)
                    {
                        progressLabel.text = error;
                        progressLabel.style.color = new Color(0.9f, 0.4f, 0.4f);
                        setupBtn.SetEnabled(true);
                        Debug.LogError($"[UniClaude] Sidecar setup failed: {error}");
                    }
                    else
                    {
#if UNITY_EDITOR_OSX
                        Debug.Log("[UniClaude] Cleared macOS quarantine flags from sidecar dependencies (some SDK binaries are not notarized by Apple).");
#endif
                        _setupContainer.RemoveFromHierarchy();
                        _setupContainer = null;
                        ShowSetupPanel(SetupState.Verifying);
                    }
                };
                card.Add(setupBtn);
            }
            else if (state == SetupState.Verifying)
            {
                var statusLabel = new Label("Verifying setup...");
                statusLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                statusLabel.style.marginBottom = 12;
                card.Add(statusLabel);

                var spinner = new Label("/");
                spinner.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Title);
                spinner.style.unityTextAlign = TextAnchor.MiddleCenter;
                spinner.style.marginBottom = 12;
                card.Add(spinner);

                int frame = 0;
                var spinnerFrames = new[] { "/", "\u2014", "\\", "|" };
                var spinnerSchedule = spinner.schedule.Execute(() =>
                {
                    frame = (frame + 1) % spinnerFrames.Length;
                    spinner.text = spinnerFrames[frame];
                }).Every(150);

                // Auto-start verification
                EditorApplication.delayCall += () => RunVerification(statusLabel, spinner, spinnerSchedule);
            }
            else if (state == SetupState.AuthMissing)
            {
                var helpBox = new HelpBox(
                    "Claude CLI is not authenticated.\n\n" +
                    "Run this command in your terminal to sign in:\n" +
                    "  claude login\n\n" +
                    "After signing in, click Check Again.",
                    HelpBoxMessageType.Warning);
                helpBox.style.marginBottom = 12;
                card.Add(helpBox);

                var checkBtn = new Button(() =>
                {
                    _setupContainer.RemoveFromHierarchy();
                    _setupContainer = null;
                    ShowSetupPanel(SetupState.Verifying);
                }) { text = "Check Again" };
                checkBtn.style.height = 30;
                checkBtn.style.marginBottom = 4;
                card.Add(checkBtn);
            }

            _setupContainer.Add(card);
            rootVisualElement.Insert(0, _setupContainer);
        }

        // ── Verification ──

        async void RunVerification(Label statusLabel, Label spinner, IVisualElementScheduledItem spinnerSchedule)
        {
            try
            {
                var mcpPort = _mcpServer?.Port ?? 0;
                await _sidecar.EnsureRunning(mcpPort, _settings);
                if (this == null) return; // Window closed during await

                // Health check — confirm sidecar responds
                var healthy = await SidecarManager.CheckHealthPublic(_sidecar.Port);
                if (!healthy)
                {
                    ShowVerificationFailure(statusLabel, spinner, spinnerSchedule,
                        "Sidecar started but is not responding. Check the Console for errors.");
                    return;
                }

                // Success — transition to chat
                spinnerSchedule?.Pause();
                spinner.text = "\u2713";
                spinner.style.color = new Color(0.4f, 0.8f, 0.4f);
                statusLabel.text = "Ready!";

                EditorApplication.delayCall += () =>
                {
                    if (_setupContainer != null)
                    {
                        _setupContainer.RemoveFromHierarchy();
                        _setupContainer = null;
                    }
                    if (_mainContainer != null)
                        _mainContainer.style.display = DisplayStyle.Flex;

                    _client = new SidecarClient(_sidecar.Port);
                    WireClientEvents();
                    _client.ConnectStream();
                    RestoreStateAfterReload();

                    if (_conversation.Messages.Count == 0)
                        _chatPanel?.AddSystemMessage(
                            "Ready! Type a message or use /help to see available commands.");
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniClaude] Verification failed: {ex}");
                ShowVerificationFailure(statusLabel, spinner, spinnerSchedule, ex.Message);
            }
        }

        void ShowVerificationFailure(Label statusLabel, Label spinner,
            IVisualElementScheduledItem spinnerSchedule, string message)
        {
            spinnerSchedule?.Pause();
            spinner.text = "\u2716";
            spinner.style.color = new Color(0.9f, 0.4f, 0.4f);

            statusLabel.text = $"Verification failed: {message}";
            statusLabel.style.color = new Color(0.9f, 0.4f, 0.4f);

            // Add retry button to the existing card
            var card = spinner.parent;
            var retryBtn = new Button(() =>
            {
                _setupContainer.RemoveFromHierarchy();
                _setupContainer = null;
                ShowSetupPanel(SetupState.Verifying);
            }) { text = "Retry" };
            retryBtn.style.height = 30;
            retryBtn.style.marginTop = 8;
            card.Add(retryBtn);
        }

        // ── Sidecar Lifecycle ──


        void WireClientEvents()
        {
            _client.OnToken += OnStreamToken;
            _client.OnAssistantText += OnAssistantText;
            _client.OnPhaseChanged += OnPhaseChanged;
            _client.OnPermissionRequest += OnPermissionRequest;
            _client.OnToolExecuted += OnMCPToolExecuted_Sidecar;
            _client.OnResult += OnQueryResult;
            _client.OnError += OnQueryError;
            _client.OnDisconnected += OnSidecarDisconnected;
            _client.OnPlanModeChanged += HandlePlanModeChanged;
            _client.OnPromptSuggestion += HandlePromptSuggestion;
            _client.OnToolActivity += HandleToolActivity;
            _client.OnTaskEvent += HandleTaskEvent;
            _client.OnToolProgress += HandleToolProgress;
        }

        void DisconnectClient()
        {
            if (_client == null) return;
            _client.OnToken -= OnStreamToken;
            _client.OnAssistantText -= OnAssistantText;
            _client.OnPhaseChanged -= OnPhaseChanged;
            _client.OnPermissionRequest -= OnPermissionRequest;
            _client.OnToolExecuted -= OnMCPToolExecuted_Sidecar;
            _client.OnResult -= OnQueryResult;
            _client.OnError -= OnQueryError;
            _client.OnDisconnected -= OnSidecarDisconnected;
            _client.OnPlanModeChanged -= HandlePlanModeChanged;
            _client.OnPromptSuggestion -= HandlePromptSuggestion;
            _client.OnToolActivity -= HandleToolActivity;
            _client.OnTaskEvent -= HandleTaskEvent;
            _client.OnToolProgress -= HandleToolProgress;
            _client.Dispose();
            _client = null;
        }

        void SaveStateBeforeReload()
        {
            SessionState.SetBool(SessionKey_IsGenerating, _isGenerating);
            SessionState.SetString(SessionKey_LastEventId, _client?.LastEventId ?? "");
            SessionState.SetString(SessionKey_StreamingContent, _chatPanel?.GetStreamingContent() ?? "");
        }

        void RestoreStateAfterReload()
        {
            var wasGenerating = SessionState.GetBool(SessionKey_IsGenerating, false);
            if (!wasGenerating) return;

            var lastEventId = SessionState.GetString(SessionKey_LastEventId, "");
            if (!string.IsNullOrEmpty(lastEventId) && _client != null)
            {
                _client.LastEventId = lastEventId;
            }

            _isGenerating = true;
            _toolbarStatus.style.display = DisplayStyle.Flex;

            // Restore streaming content accumulated before reload
            var savedContent = SessionState.GetString(SessionKey_StreamingContent, "");
            if (!string.IsNullOrEmpty(savedContent))
                _chatPanel.SetStreamingContent(savedContent);

            _chatPanel.ShowThinking();

            // Reconnect SSE — Last-Event-ID will trigger replay of missed events
            _client.ConnectStream();

            _chatPanel.AddInfoBubble(new ChatMessage(MessageRole.Info, "Domain reloaded \u2014 reconnected to sidecar"));

            // Clear session state
            SessionState.SetBool(SessionKey_IsGenerating, false);
            SessionState.SetString(SessionKey_LastEventId, "");
            SessionState.SetString(SessionKey_StreamingContent, "");
        }

        async Task TryReconnect()
        {
            _reconnectAttempt++;
            if (_reconnectAttempt > MaxReconnectAttempts)
            {
                _chatPanel.AddSystemMessage(
                    "Connection to sidecar lost after multiple retries. Reopen the window to reconnect.");
                _reconnectAttempt = 0;
                return;
            }
            var delayMs = 1000 * (1 << _reconnectAttempt);
            _chatPanel.AddSystemMessage(
                $"Connection lost. Reconnecting in {delayMs / 1000}s " +
                $"(attempt {_reconnectAttempt}/{MaxReconnectAttempts})...");
            await Task.Delay(delayMs);
            try
            {
                var mcpPort = _mcpServer?.Port ?? 0;
                await _sidecar.EnsureRunning(mcpPort, _settings);
                DisconnectClient();
                _client = new SidecarClient(_sidecar.Port);
                WireClientEvents();
                _client.ConnectStream();
                _reconnectAttempt = 0;
                _chatPanel.AddSystemMessage("Reconnected to sidecar.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[UniClaude] Reconnect attempt {_reconnectAttempt} failed: {ex.Message}");
                if (_reconnectAttempt < MaxReconnectAttempts)
                    _ = TryReconnect();
                else
                {
                    _chatPanel.AddSystemMessage(
                        "Connection to sidecar lost after multiple retries. Reopen the window to reconnect.");
                    _reconnectAttempt = 0;
                }
            }
        }

        // ── Sidecar Event Handlers ──

        void OnStreamToken(string token)
        {
            EditorApplication.delayCall += () => _chatPanel.AppendStreamingToken(token);
        }

        void OnAssistantText(string text)
        {
            EditorApplication.delayCall += () =>
            {
                if (!_isGenerating) return;
                _chatPanel.FinalizeStreamingMessage(text, _conversation);
            };
        }

        void OnPhaseChanged(string phase, string toolName)
        {
            EditorApplication.delayCall += () =>
            {
                var p = phase switch
                {
                    "thinking" => StreamPhase.Thinking,
                    "writing" => StreamPhase.Writing,
                    "tool_use" => StreamPhase.ToolUse,
                    _ => StreamPhase.None,
                };
                _chatPanel.UpdatePhase(p, toolName);
            };
        }

        void OnPermissionRequest(SidecarEvent evt)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                _chatPanel.HandlePermissionRequest(evt, _client);
            };
        }

        /// <summary>
        /// Handles a tool execution event forwarded from the sidecar client.
        /// Adapts the sidecar result into an <see cref="MCPToolResult"/> and delegates to
        /// <see cref="OnMCPToolExecuted"/>.
        /// </summary>
        void OnMCPToolExecuted_Sidecar(string tool, string result, bool success)
        {
            EditorApplication.delayCall += () =>
            {
                var mcpResult = success
                    ? MCPToolResult.Success(result)
                    : MCPToolResult.Error(result);
                OnMCPToolExecuted(tool, "{}", mcpResult);
            };
        }

        void OnQueryResult(SidecarEvent evt)
        {
            EditorApplication.delayCall += () =>
            {
                if (!_isGenerating) return;

                if (!string.IsNullOrEmpty(evt.SessionId))
                    _conversation.SessionId = evt.SessionId;

                _chatPanel.HandleQueryResult(evt, _conversation, _currentActivity);
                _currentActivity = null;
                OnGenerationComplete();
            };
        }

        void OnQueryError(string message)
        {
            EditorApplication.delayCall += () =>
            {
                if (!_isGenerating) return;

                if (IsAuthError(message))
                {
                    _currentActivity = null;
                    OnGenerationComplete();
                    DisconnectClient();
                    ShowSetupPanel(SetupState.AuthMissing);
                    return;
                }

                _chatPanel.HandleQueryError(message, _conversation);
                _currentActivity = null;
                OnGenerationComplete();
            };
        }

        internal static bool IsAuthError(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            var lower = message.ToLowerInvariant();
            return lower.Contains("authentication") ||
                   lower.Contains("unauthorized") ||
                   lower.Contains("not authenticated") ||
                   lower.Contains("api key") ||
                   lower.Contains("invalid_api_key") ||
                   lower.Contains("claude login") ||
                   (lower.Contains("permission denied") && lower.Contains("api"));
        }

        void OnSidecarDisconnected()
        {
            EditorApplication.delayCall += () =>
            {
                OnGenerationComplete();
                _ = TryReconnect();
            };
        }

        void HandlePlanModeChanged(bool active)
        {
            _planMode = active;
            RefreshHintText();
            _chatPanel.AddSystemMessage(active
                ? "Agent entered plan mode"
                : "Agent exited plan mode");
        }

        void HandlePromptSuggestion(string suggestion)
        {
            _inputController.SetSuggestion(suggestion);
        }

        void HandleToolActivity(SidecarEvent evt)
        {
            EditorApplication.delayCall += () =>
                _chatPanel.HandleToolActivity(evt, _currentActivity);
        }

        void HandleTaskEvent(SidecarEvent evt)
        {
            EditorApplication.delayCall += () =>
                _chatPanel.HandleTaskEvent(evt, _currentActivity);
        }

        void HandleToolProgress(SidecarEvent evt)
        {
            _chatPanel.HandleToolProgress(evt);
        }

        /// <summary>
        /// Handles a tool execution event from the MCP server. Adds a tool-call bubble to
        /// the chat and records the message in the conversation.
        /// </summary>
        /// <param name="toolName">Name of the tool that was executed.</param>
        /// <param name="argsJson">JSON-serialized arguments passed to the tool.</param>
        /// <param name="result">The result returned by the tool.</param>
        void OnMCPToolExecuted(string toolName, string argsJson, MCPToolResult result)
        {
            var msg = new ChatMessage(MessageRole.ToolCall, result.Text)
            {
                ToolName = toolName,
                IsError = result.IsError,
            };
            _conversation.AddMessage(msg);
            _chatPanel.AddToolCallBubble(msg);
        }

        /// <summary>
        /// Handles a log message emitted by the MCP server's reload strategy.
        /// Displays it as an info bubble in the chat.
        /// </summary>
        /// <param name="message">The log message text.</param>
        void OnMCPLog(string message)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                var msg = new ChatMessage(MessageRole.Info, message);
                _conversation.AddMessage(msg);
                _chatPanel.AddInfoBubble(msg);
            };
        }

        void OnGenerationComplete()
        {
            _isGenerating = false;
            _toolbarStatus.style.display = DisplayStyle.None;
            _chatPanel.OnGenerationComplete();
            SaveCurrentConversation();
            MCPServer.Instance?.NotifyTurnComplete();
            // Safety net: refresh assets unconditionally so that script edits made via
            // built-in SDK tools (which bypass MCP) still trigger domain reload.
            AssetDatabase.Refresh();
        }

        // ── Input Handling ──

        void HandleInputSubmit(MessageSubmission submission)
        {
            _inputController.Clear();

            var text = submission.Text;

            if (_isGenerating)
            {
                _ = InterruptAndSendAsync(text, submission.Attachments);
                return;
            }

            if (!string.IsNullOrEmpty(text) && text[0] == '/')
            {
                var cmd = _commands.Parse(text);
                if (cmd == null)
                {
                    _chatPanel.AddSystemMessage(
                        $"Unknown command: {text.Split(' ')[0]}. Type /help for available commands.");
                    return;
                }
                if (cmd.Source == CommandSource.Local)
                {
                    cmd.Execute?.Invoke(SlashCommandRegistry.ParseArgs(text));
                    return;
                }

                // CLI command with a known file path — read the prompt body and send that
                if (cmd.FilePath != null)
                {
                    var body = SlashCommandRegistry.ReadCommandBody(cmd.FilePath);
                    if (body != null)
                    {
                        SendMessage(body);
                        return;
                    }
                    _chatPanel.AddSystemMessage($"Could not read command file: {cmd.FilePath}");
                    return;
                }
            }

            SendMessage(text, submission.Attachments);
        }

        void HandleCancelRequested()
        {
            if (!_isGenerating || _client == null) return;
            _chatPanel.HideThinking();
            _ = CancelGenerationAsync();
        }

        async Task CancelGenerationAsync()
        {
            try
            {
                await _client.Cancel();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UniClaude] Cancel failed: {ex.Message}");
            }
            _chatPanel.AddSystemMessage("Cancelled by user.");
            OnGenerationComplete();
        }

        async void SendMessage(string text, IReadOnlyList<AttachmentInfo> attachments = null)
        {
            _inputController.ClearSuggestion();

            // Cooldown: reject sends within 500ms of the last one
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastSendTime < 0.5)
                return;
            _lastSendTime = now;

            if (_client == null)
            {
                _chatPanel.AddSystemMessage("Sidecar not connected. Waiting for startup...");
                return;
            }

            // Build display text with attachment names
            var displayText = text;
            if (attachments != null && attachments.Count > 0)
            {
                var names = new List<string>();
                foreach (var att in attachments)
                    names.Add(att.FileName);
                var attachmentSuffix = $"\n[Attached: {string.Join(", ", names)}]";
                displayText = string.IsNullOrEmpty(text) ? attachmentSuffix.TrimStart('\n') : text + attachmentSuffix;
            }

            _conversation.AddMessage(new ChatMessage(MessageRole.User, displayText));
            _chatPanel.AddMessageBubble(_conversation.Messages[^1]);
            _toolbarTitle.text = $"UniClaude \u2014 {_conversation.Title}";

            _isGenerating = true;
            _toolbarStatus.style.display = DisplayStyle.Flex;
            _chatPanel.ShowThinking();
            _currentActivity = new ActivityLog();

            try
            {
                string systemPrompt = null;
                if (_settings.ProjectAwarenessEnabled && _projectAwareness != null)
                {
                    systemPrompt = _projectAwareness.GetTier1Context();
                    if (systemPrompt != null)
                        _chatPanel.AddTier1Indicator(systemPrompt);
                }

                List<SidecarAttachment> sidecarAttachments = null;
                if (attachments != null && attachments.Count > 0)
                {
                    sidecarAttachments = new List<SidecarAttachment>();
                    foreach (var att in attachments)
                    {
                        try
                        {
                            if (AttachmentManager.IsImageExtension(att.Extension))
                            {
                                var bytes = System.IO.File.ReadAllBytes(att.OriginalPath);
                                var base64 = System.Convert.ToBase64String(bytes);
                                var mediaType = att.Extension.ToLowerInvariant() switch
                                {
                                    ".png" => "image/png",
                                    ".jpg" or ".jpeg" => "image/jpeg",
                                    ".gif" => "image/gif",
                                    _ => "image/png" // unsupported formats transcoded upstream or fallback
                                };
                                sidecarAttachments.Add(new SidecarAttachment
                                {
                                    Type = "image",
                                    FileName = att.FileName,
                                    Content = base64,
                                    MediaType = mediaType
                                });
                            }
                            else
                            {
                                var content = System.IO.File.ReadAllText(att.OriginalPath);
                                sidecarAttachments.Add(new SidecarAttachment
                                {
                                    Type = "text",
                                    FileName = att.FileName,
                                    Content = content
                                });
                            }
                        }
                        catch (System.IO.IOException ex)
                        {
                            Debug.LogWarning($"[UniClaude] Skipping attachment {att.FileName}: {ex.Message}");
                        }
                    }
                }

                await _client.StartChat(text,
                    model: _currentModel,
                    effort: _currentEffort,
                    sessionId: _conversation.SessionId,
                    systemPrompt: systemPrompt,
                    autoAllowMCPTools: _settings.AutoAllowMCPTools,
                    planMode: _planMode,
                    attachments: sidecarAttachments);
            }
            catch (Exception ex)
            {
                _chatPanel.HideThinking();
                var errorMsg = $"Error: {ex.Message}";
                _conversation.AddMessage(new ChatMessage(MessageRole.System, errorMsg));
                _chatPanel.AddMessageBubble(_conversation.Messages[^1]);
                Debug.LogException(ex);
                OnGenerationComplete();
            }
        }

        async Task InterruptAndSendAsync(string text, IReadOnlyList<AttachmentInfo> attachments = null)
        {
            if (_client != null)
            {
                _chatPanel.AddSystemMessage("Interrupting current query...");
                await _client.Cancel();
            }
            _chatPanel.HideThinking();
            OnGenerationComplete();
            await Task.Delay(300);
            SendMessage(text, attachments);
        }

        // ── Settings Event Handlers ──

        void HandleFontSizeChanged(string preset)
        {
            _settings.ChatFontSize = preset;
            _theme.FontPreset = preset;
            UniClaudeSettings.Save(_settings);
            ApplyFontSizes();
            _chatPanel.RebuildMessages(_conversation);
            _settingsPanel.Refresh(_settings, _currentModel, _currentEffort,
                _projectAwareness, _sidecar, _mcpServer);
        }

        void HandleModelChanged(string model)
        {
            _currentModel = model;
            _settings.SelectedModel = model;
            UniClaudeSettings.Save(_settings);
            RefreshHintText();
        }

        void HandleEffortChanged(string effort)
        {
            _currentEffort = effort;
            _settings.SelectedEffort = effort;
            UniClaudeSettings.Save(_settings);
            RefreshHintText();
        }

        void HandleProjectAwarenessToggled()
        {
            if (_settings.ProjectAwarenessEnabled && _projectAwareness == null)
            {
                _projectAwareness = new ProjectAwareness();
                _projectAwareness.Initialize(
                    System.IO.Path.GetDirectoryName(Application.dataPath));
            }
            else if (!_settings.ProjectAwarenessEnabled && _projectAwareness != null)
            {
                _projectAwareness.Dispose();
                _projectAwareness = null;
            }
        }

        void HandleIndexRebuildRequested()
        {
            if (_projectAwareness == null)
            {
                _chatPanel.AddSystemMessage(
                    "Project awareness is not initialized. Enable it in Settings.");
                return;
            }
            _chatPanel.AddSystemMessage(_projectAwareness.FullRebuild());
        }

        void HandleIndexClearRequested()
        {
            if (_projectAwareness == null)
            {
                _chatPanel.AddSystemMessage(
                    "Project awareness is not initialized.");
                return;
            }
            _projectAwareness.Dispose();
            _projectAwareness = null;
            _chatPanel.AddSystemMessage(
                "Project index cleared. Re-enable Project Awareness in Settings to rebuild.");
        }

        void HandleCachePurgeRequested()
        {
            StartNewChat();
        }

        async void HandleSidecarRestart()
        {
            DisconnectClient();
            EditorApplication.update -= _sidecar.HealthPing;
            _sidecar?.Dispose();
            _sidecar = new SidecarManager();

            var mcpPort = _mcpServer?.Port ?? 0;
            await _sidecar.EnsureRunning(mcpPort, _settings);
            if (this == null) return;

            _client = new SidecarClient(_sidecar.Port);
            WireClientEvents();
            _client.ConnectStream();
            EditorApplication.update += _sidecar.HealthPing;
            _settingsPanel.Refresh(_settings, _currentModel, _currentEffort,
                _projectAwareness, _sidecar, _mcpServer);
            Debug.Log("[UniClaude] Sidecar restarted.");
        }

        // ── History Event Handlers ──

        void LoadConversation(string id)
        {
            SaveCurrentConversation();
            _inputController.Clear();
            _conversation = ConversationStore.Load(id) ?? new Conversation();
            _planMode = false;
            _toolbarTitle.text = $"UniClaude \u2014 {_conversation.Title}";
            _chatPanel.RebuildMessages(_conversation);
            RefreshHintText();
            SwitchTab(Tab.Chat);
        }

        void StartNewChat()
        {
            SaveCurrentConversation();
            _inputController.Clear();
            _conversation = new Conversation();
            _planMode = false;
            _toolbarTitle.text = $"UniClaude \u2014 {_conversation.Title}";
            _chatPanel.RebuildMessages(_conversation);
            RefreshHintText();
            SwitchTab(Tab.Chat);
        }

        void ClearAllConversations()
        {
            ConversationStore.DeleteAll();
            StartNewChat();
            _historyPanel.Refresh(_conversation.Id);
        }

        // ── Helpers ──

        void RefreshHintText()
        {
            var modelLabel = ResolveModelLabel(_currentModel);
            var effortLabel = ResolveEffortLabel(_currentEffort);
            _inputController.UpdateHintText(modelLabel, effortLabel, _planMode);
        }

        string ResolveModelLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Default";
            var match = UniClaudeSettings.ModelChoices.Find(m => m.Value == value);
            return match.Label ?? value;
        }

        string ResolveEffortLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Default";
            var match = UniClaudeSettings.EffortChoices.Find(e => e.Value == value);
            return match.Label ?? value;
        }

        void ApplyFontSizes()
        {
            if (_toolbarTitle != null)
                _toolbarTitle.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            if (_toolbarStatus != null)
                _toolbarStatus.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            if (_tabChat != null)
                _tabChat.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Tab);
            if (_tabHistory != null)
                _tabHistory.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Tab);
            if (_tabSettings != null)
                _tabSettings.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Tab);
            _inputController?.ApplyFontSizes();
        }

        void SaveCurrentConversation()
        {
            if (_conversation.Messages.Count > 0)
                ConversationStore.Save(_conversation);
        }

        // ── Commands ──

        /// <summary>
        /// Registers all local slash commands handled in-editor (not forwarded to the sidecar).
        /// </summary>
        void RegisterLocalCommands()
        {
            _commands.RegisterLocal("clear", "Clear conversation history", _ =>
            {
                SaveCurrentConversation();
                _conversation = new Conversation();
                _planMode = false;
                _toolbarTitle.text = $"UniClaude \u2014 {_conversation.Title}";
                _chatPanel.RebuildMessages(_conversation);
                RefreshHintText();
                _chatPanel.AddSystemMessage("Conversation cleared.");
            });

            _commands.RegisterLocal("new", "Start a new chat", _ => StartNewChat());

            _commands.RegisterLocal("help", "Show available commands", _ =>
            {
                var lines = new System.Text.StringBuilder();
                lines.AppendLine("Available commands:\n");
                foreach (var cmd in _commands.Commands)
                {
                    var source = cmd.Source == CommandSource.Local ? "" : " (CLI)";
                    lines.AppendLine($"  /{cmd.Name} \u2014 {cmd.Description}{source}");
                }
                lines.AppendLine("\nType / and press Tab to autocomplete.");
                _chatPanel.AddSystemMessage(lines.ToString());
            });

            var modelCmd = new SlashCommand
            {
                Name = "model",
                Description = "Switch the AI model",
                AcceptsArgs = true,
                Source = CommandSource.Local,
                ArgChoices = UniClaudeSettings.ModelChoices
                    .Select(m => new ArgChoice
                        { Value = m.Value, Label = m.Label, Description = m.Description })
                    .ToList(),
                Execute = args =>
                {
                    if (string.IsNullOrWhiteSpace(args))
                    {
                        _chatPanel.AddSystemMessage(
                            $"Current model: {_currentModel ?? "default"}\nUsage: /model <name>");
                        return;
                    }
                    HandleModelChanged(args.Trim());
                    _chatPanel.AddSystemMessage($"Model set to: {_currentModel}");
                }
            };
            _commands.RegisterLocal(modelCmd);

            var effortCmd = new SlashCommand
            {
                Name = "effort",
                Description = "Switch the reasoning effort level",
                AcceptsArgs = true,
                Source = CommandSource.Local,
                ArgChoices = UniClaudeSettings.EffortChoices
                    .Select(e => new ArgChoice
                        { Value = e.Value, Label = e.Label, Description = e.Description })
                    .ToList(),
                Execute = args =>
                {
                    if (string.IsNullOrWhiteSpace(args))
                    {
                        _chatPanel.AddSystemMessage(
                            $"Current effort: {_currentEffort ?? "default"}\nUsage: /effort <level>");
                        return;
                    }
                    HandleEffortChanged(args.Trim());
                    _chatPanel.AddSystemMessage($"Effort set to: {_currentEffort}");
                }
            };
            _commands.RegisterLocal(effortCmd);

            _commands.RegisterLocal("status", "Show sidecar status and session info", _ =>
            {
                var sidecarStatus = _sidecar is { IsRunning: true }
                    ? $"running (port {_sidecar.Port})" : "not running";
                _chatPanel.AddSystemMessage(
                    $"Sidecar: {sidecarStatus}\n" +
                    $"Model: {_currentModel ?? "default"}\n" +
                    $"Effort: {_currentEffort ?? "default"}\n" +
                    $"Messages: {_conversation.Messages.Count}");
            });

            _commands.RegisterLocal("compact",
                "Summarize conversation to reduce context", _ =>
                _chatPanel.AddSystemMessage(
                    "Compact is not yet implemented for UniClaude sessions."));

            _commands.RegisterLocal("refresh",
                "Re-scan available slash commands", _ =>
                {
                    _commands.DiscoverCliCommands();
                    _chatPanel.AddSystemMessage(
                        $"Refreshed. {_commands.Commands.Count} commands available.");
                });

            _commands.RegisterLocal("index",
                "Rebuild project index for context awareness", _ =>
                {
                    if (_projectAwareness == null)
                    {
                        _chatPanel.AddSystemMessage(
                            "Project awareness is not initialized. Enable it in Settings.");
                        return;
                    }
                    _chatPanel.AddSystemMessage(_projectAwareness.FullRebuild());
                });

            _commands.RegisterLocal("plan",
                "Toggle plan mode (agent proposes without executing)", _ =>
                {
                    _planMode = !_planMode;
                    RefreshHintText();
                    _chatPanel.AddSystemMessage(_planMode
                        ? "Plan mode enabled \u2014 agent will propose actions without executing"
                        : "Plan mode disabled");
                });

            _commands.RegisterLocal("undo",
                "Revert file changes from last agent turn", args =>
                {
                    if (_client == null)
                    {
                        _chatPanel.AddSystemMessage("Sidecar not connected.");
                        return;
                    }
                    _chatPanel.AddSystemMessage("Undoing last file changes...");
                    _ = ExecuteUndoAsync();
                });

            var healthcheckCmd = new SlashCommand
            {
                Name = "healthcheck",
                Description = "Run integration health check on all MCP tools",
                AcceptsArgs = true,
                Source = CommandSource.Local,
                ArgChoices = new System.Collections.Generic.List<ArgChoice>
                {
                    new() { Value = "light", Label = "Light", Description = "Quick smoke test (~5 tool calls)" },
                    new() { Value = "complete", Label = "Complete", Description = "Full suite (~25 tool calls)" },
                },
                Execute = args => HandleHealthCheck(args?.Trim()),
            };
            _commands.RegisterLocal(healthcheckCmd);
        }

        void HandleHealthCheck(string tier)
        {
            if (string.IsNullOrWhiteSpace(tier))
            {
                _chatPanel.AddSystemMessage("Usage: /healthcheck light  or  /healthcheck complete");
                return;
            }

            if (_client == null)
            {
                _chatPanel.AddSystemMessage("Sidecar not connected. Cannot run health check.");
                return;
            }

            if (_healthCheckRunner != null)
            {
                _chatPanel.AddSystemMessage("A health check is already running.");
                return;
            }

            var isLight = tier.Equals("light", StringComparison.OrdinalIgnoreCase);
            var isComplete = tier.Equals("complete", StringComparison.OrdinalIgnoreCase);

            if (!isLight && !isComplete)
            {
                _chatPanel.AddSystemMessage("Unknown tier. Use: /healthcheck light  or  /healthcheck complete");
                return;
            }

            var steps = isLight ? HealthCheckSteps.Light : HealthCheckSteps.Complete;
            var tierLabel = isLight ? "Light" : "Complete";
            var estimatedCost = isLight ? "~$0.02-0.05" : "~$0.10-0.15";
            var estimatedTime = isLight ? "~15 seconds" : "~60 seconds";

            // Build confirmation message with buttons
            var container = new VisualElement();
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var msgLabel = new Label(
                $"Package Health Check \u2014 {tierLabel}\n\n" +
                $"This will send {steps.Count} messages to Claude, exercising " +
                (isLight ? "core" : "all") + " MCP tool categories. " +
                $"Temporary test assets will be created in\n" +
                $"Assets/UniClaudeHealthCheck/ and cleaned up after.\n\n" +
                $"Estimated cost: {estimatedCost} in API tokens\n" +
                $"Estimated time: {estimatedTime}");
            msgLabel.style.whiteSpace = WhiteSpace.Normal;
            msgLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            container.Add(msgLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 8;

            var runBtn = new Button() { text = "Run" };
            runBtn.style.marginRight = 8;

            var cancelBtn = new Button() { text = "Cancel" };

            runBtn.clicked += () =>
            {
                container.RemoveFromHierarchy();
                _ = RunHealthCheckAsync(steps);
            };

            cancelBtn.clicked += () =>
            {
                container.RemoveFromHierarchy();
                _chatPanel.AddSystemMessage("Health check cancelled.");
            };

            btnRow.Add(runBtn);
            btnRow.Add(cancelBtn);
            container.Add(btnRow);

            _chatPanel.AddSystemMessage($"Health check requested \u2014 {tierLabel}");
            _chatPanel.InsertElement(container);
        }

        async Task RunHealthCheckAsync(System.Collections.Generic.List<HealthCheckStep> steps)
        {
            _healthCheckRunner = new HealthCheckRunner(
                _client, _chatPanel, _currentModel, _currentEffort, steps);

            try
            {
                await _healthCheckRunner.RunAsync();
            }
            catch (Exception ex)
            {
                _chatPanel.AddSystemMessage($"Health check error: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                _healthCheckRunner = null;
            }
        }

        async Task ExecuteUndoAsync()
        {
            try
            {
                var result = await _client.Undo();
                _chatPanel.AddSystemMessage(result.Success
                    ? result.Message
                    : $"Undo failed: {result.Message}");
            }
            catch (Exception ex)
            {
                _chatPanel.AddSystemMessage($"Undo error: {ex.Message}");
            }
        }
    }
}
