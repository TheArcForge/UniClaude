using System;
using Newtonsoft.Json.Linq;
using UniClaude.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// A <see cref="VisualElement"/> that implements the chat view: a scroll view containing
    /// message bubbles, a welcome screen, a thinking indicator, and streaming display.
    /// Handles all rendering of conversation messages and live generation state.
    /// ChatPanel does NOT own the Conversation — it receives it as a parameter and renders
    /// from it.
    /// </summary>
    public class ChatPanel : VisualElement
    {
        // ── Fields ───────────────────────────────────────────────────────────

        readonly ThemeContext _theme;
        ScrollView _scrollView;
        VisualElement _welcomePanel;
        ThinkingIndicator _thinkingIndicator;
        string _streamingContent = "";
        bool _autoScroll = true;

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the user aborts generation via the permission prompt's Abort button.
        /// </summary>
        public event Action OnAbortRequested;

        /// <summary>
        /// Fired when the user clicks a starter suggestion on the welcome panel.
        /// </summary>
        public event Action<string> OnStarterClicked;

        // ── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds the chat panel: scroll view, welcome screen, and thinking indicator.
        /// </summary>
        /// <param name="theme">Theme context supplying colors and font sizes.</param>
        public ChatPanel(ThemeContext theme)
        {
            _theme = theme;

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            Build();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a System <see cref="ChatMessage"/>, renders it as a bubble, and scrolls to bottom.
        /// NOTE: Does NOT add to the conversation — that is UniClaudeWindow's responsibility.
        /// </summary>
        /// <param name="text">The system message text to render.</param>
        public void AddSystemMessage(string text)
        {
            var msg = new ChatMessage(MessageRole.System, text);
            AddMessageBubble(msg);
            ScrollToBottom();
        }

        /// <summary>
        /// Inserts a raw VisualElement into the message list and scrolls to it.
        /// Used for custom UI like confirmation dialogs.
        /// </summary>
        /// <param name="element">The element to insert.</param>
        public void InsertElement(VisualElement element)
        {
            _scrollView.Add(element);
            ScrollToBottom();
        }

        /// <summary>
        /// Renders any message as a bubble and hides the welcome panel.
        /// Uses <see cref="MessageRenderer"/> for styling appropriate to the role.
        /// For Assistant messages with an Activity log, also renders the persisted activity bubble.
        /// </summary>
        /// <param name="message">The message to render.</param>
        public void AddMessageBubble(ChatMessage message)
        {
            _welcomePanel.style.display = DisplayStyle.None;

            bool isUser   = message.Role == MessageRole.User;
            bool isSystem = message.Role == MessageRole.System;

            var bubble = MessageRenderer.CreateBubble(_theme, message.Role);

            string roleText = isUser ? "You" : isSystem ? "System" : "Claude";
            bubble.Add(MessageRenderer.MakeRoleLabel(_theme, roleText));

            var textColor = _theme.IsDark ? new Color(0.85f, 0.85f, 0.85f) : Color.black;

            if (isUser || isSystem)
            {
                bubble.Add(MessageRenderer.MakeSelectableText(
                    _theme, message.Content, textColor,
                    fontSize: _theme.FontSize(ThemeContext.FontTier.Body)));
            }
            else if (message.Content != null && message.Content.Contains("```"))
            {
                MessageRenderer.AddContentWithCodeBlocks(bubble, message.Content, _theme);
            }
            else
            {
                MessageRenderer.AddTextWithFileLinks(
                    bubble, message.Content, textColor,
                    _theme.FontSize(ThemeContext.FontTier.Body), _theme);
            }

            InsertBeforeThinking(bubble);

            // Show persisted activity log for assistant messages
            if (message.Role == MessageRole.Assistant
                && message.Activity != null
                && message.Activity.HasEntries)
            {
                message.Activity.RebuildTaskMap();
                AddPersistedActivityBubble(message.Activity);
            }
        }

        /// <summary>
        /// Renders a collapsible tool call entry from a persisted <see cref="ChatMessage"/>.
        /// Shows a header button with status icon and tool name; expands to show the result.
        /// </summary>
        /// <param name="msg">The ToolCall message to render.</param>
        public void AddToolCallBubble(ChatMessage msg)
        {
            var container = new VisualElement();
            container.style.marginLeft  = 12;
            container.style.marginRight = 12;
            container.style.marginBottom = 4;

            var status = msg.IsError ? "\u2717" : "\u2713";
            var statusColor = msg.IsError
                ? new Color(0.9f, 0.4f, 0.4f)
                : new Color(0.4f, 0.8f, 0.4f);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems    = Align.Center;

            var header = new Button { text = $"\u25b6 {status} {msg.ToolName}" };
            header.style.flexGrow          = 1;
            header.style.backgroundColor   = Color.clear;
            header.style.borderTopWidth    = 0;
            header.style.borderBottomWidth = 0;
            header.style.borderLeftWidth   = 0;
            header.style.borderRightWidth  = 0;
            header.style.color             = statusColor;
            header.style.fontSize          = _theme.FontSize(ThemeContext.FontTier.Meta);
            header.style.unityTextAlign    = TextAnchor.MiddleLeft;
            header.style.paddingLeft       = 4;
            header.style.height            = 20;
            headerRow.Add(header);

            var detailText = $"Result:\n{msg.Content}";
            var detail = new Label(detailText);
            detail.style.display             = DisplayStyle.None;
            detail.style.fontSize            = _theme.FontSize(ThemeContext.FontTier.Meta);
            detail.style.whiteSpace          = WhiteSpace.PreWrap;
            detail.style.backgroundColor     = _theme.IsDark
                ? new Color(0.14f, 0.14f, 0.16f)
                : new Color(0.92f, 0.92f, 0.92f);
            detail.style.paddingTop          = 8;
            detail.style.paddingBottom       = 8;
            detail.style.paddingLeft         = 8;
            detail.style.paddingRight        = 8;
            detail.style.borderTopLeftRadius     = 4;
            detail.style.borderTopRightRadius    = 4;
            detail.style.borderBottomLeftRadius  = 4;
            detail.style.borderBottomRightRadius = 4;
            detail.style.marginTop           = 4;
            detail.style.color               = _theme.IsDark
                ? new Color(0.6f, 0.6f, 0.65f)
                : new Color(0.35f, 0.35f, 0.4f);
            detail.enableRichText = false;

            bool expanded = false;
            header.clicked += () =>
            {
                expanded = !expanded;
                detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                header.text = (expanded ? "\u25bc" : "\u25b6") + $" {status} {msg.ToolName}";
            };

            // Add "Review Diff" button for file_modify_script results
            if (msg.ToolName == "file_modify_script" && !msg.IsError)
            {
                string diffText = null;
                string filePath = null;
                try
                {
                    var resultObj = Newtonsoft.Json.Linq.JObject.Parse(msg.Content);
                    diffText = resultObj["diff"]?.ToString();
                    filePath = resultObj["path"]?.ToString() ?? "unknown";
                }
                catch { /* Not JSON or missing diff field — skip button */ }

                if (!string.IsNullOrEmpty(diffText))
                {
                    var diffBtn = new Button(() =>
                    {
                        DiffViewerWindow.Show(filePath, diffText);
                    }) { text = "Review Diff" };
                    diffBtn.style.height = 18;
                    diffBtn.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
                    diffBtn.style.backgroundColor = Color.clear;
                    diffBtn.style.borderTopWidth = 0;
                    diffBtn.style.borderBottomWidth = 0;
                    diffBtn.style.borderLeftWidth = 0;
                    diffBtn.style.borderRightWidth = 0;
                    diffBtn.style.color = _theme.IsDark
                        ? new Color(0.5f, 0.7f, 0.9f)
                        : new Color(0.2f, 0.4f, 0.7f);
                    diffBtn.style.marginLeft = 4;
                    headerRow.Add(diffBtn);
                }
            }

            container.Add(headerRow);
            container.Add(detail);
            InsertBeforeThinking(container);
        }

        /// <summary>
        /// Renders a collapsible project context indicator from a persisted <see cref="ChatMessage"/>.
        /// The header shows a token estimate; clicking expands to show the raw content.
        /// </summary>
        /// <param name="msg">The ProjectContext message to render.</param>
        public void AddProjectContextBubble(ChatMessage msg)
        {
            var tokenEstimate = msg.Content.Length / 4 + 1;

            var container = new VisualElement();
            container.style.marginLeft   = 12;
            container.style.marginRight  = 12;
            container.style.marginBottom = 4;

            var header = new Button
            {
                text = $"\u25b6 Project context \u2014 ~{tokenEstimate} tokens"
            };
            header.style.flexGrow          = 1;
            header.style.backgroundColor   = Color.clear;
            header.style.borderTopWidth    = 0;
            header.style.borderBottomWidth = 0;
            header.style.borderLeftWidth   = 0;
            header.style.borderRightWidth  = 0;
            header.style.color             = _theme.IsDark
                ? new Color(0.5f, 0.5f, 0.55f)
                : new Color(0.4f, 0.4f, 0.45f);
            header.style.fontSize          = _theme.FontSize(ThemeContext.FontTier.Meta);
            header.style.unityTextAlign    = TextAnchor.MiddleLeft;
            header.style.paddingLeft       = 4;
            header.style.height            = 20;

            var detail = new Label(msg.Content);
            detail.style.display             = DisplayStyle.None;
            detail.style.fontSize            = _theme.FontSize(ThemeContext.FontTier.Meta);
            detail.style.whiteSpace          = WhiteSpace.PreWrap;
            detail.style.backgroundColor     = _theme.IsDark
                ? new Color(0.14f, 0.14f, 0.16f)
                : new Color(0.92f, 0.92f, 0.92f);
            detail.style.paddingTop          = 8;
            detail.style.paddingBottom       = 8;
            detail.style.paddingLeft         = 8;
            detail.style.paddingRight        = 8;
            detail.style.borderTopLeftRadius     = 4;
            detail.style.borderTopRightRadius    = 4;
            detail.style.borderBottomLeftRadius  = 4;
            detail.style.borderBottomRightRadius = 4;
            detail.style.marginTop           = 4;
            detail.style.color               = _theme.IsDark
                ? new Color(0.6f, 0.6f, 0.65f)
                : new Color(0.35f, 0.35f, 0.4f);
            detail.enableRichText = false;

            bool expanded = false;
            header.clicked += () =>
            {
                expanded = !expanded;
                detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                header.text = (expanded ? "\u25bc" : "\u25b6")
                    + $" Project context \u2014 ~{tokenEstimate} tokens";
            };

            container.Add(header);
            container.Add(detail);
            InsertBeforeThinking(container);
        }

        /// <summary>
        /// Creates a ProjectContext message and renders it as a collapsible project context bubble.
        /// Does NOT add to conversation.
        /// </summary>
        /// <param name="tier1Text">The project context text to display.</param>
        public void AddTier1Indicator(string tier1Text)
        {
            var msg = new ChatMessage(MessageRole.ProjectContext, tier1Text);
            AddProjectContextBubble(msg);
        }

        /// <summary>
        /// Renders a token usage label below the last assistant response.
        /// Does NOT add to conversation — that is UniClaudeWindow's responsibility.
        /// </summary>
        /// <param name="usage">The token usage data to render, or null (no-op).</param>
        public void AddUsageLabel(TokenUsage usage)
        {
            if (usage == null) return;

            var msg = new ChatMessage(MessageRole.TokenUsage, "")
            {
                InputTokens  = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
            };
            AddTokenUsageBubble(msg);
        }

        /// <summary>
        /// Renders a token usage label from a persisted <see cref="ChatMessage"/>.
        /// </summary>
        /// <param name="msg">The TokenUsage message to render.</param>
        public void AddTokenUsageBubble(ChatMessage msg)
        {
            var total = msg.InputTokens + msg.OutputTokens;
            var text  = $"\u2193 {msg.InputTokens:N0} in \u00b7 {msg.OutputTokens:N0} out \u00b7 {total:N0} total tokens";

            var label = new Label(text);
            label.style.fontSize       = _theme.FontSize(ThemeContext.FontTier.Hint);
            label.style.color          = _theme.IsDark
                ? new Color(0.45f, 0.45f, 0.5f)
                : new Color(0.5f, 0.5f, 0.55f);
            label.style.marginLeft     = 12;
            label.style.marginBottom   = 8;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;

            InsertBeforeThinking(label);
        }

        /// <summary>
        /// Renders an informational status line from a persisted <see cref="ChatMessage"/>.
        /// </summary>
        /// <param name="msg">The Info message to render.</param>
        public void AddInfoBubble(ChatMessage msg)
        {
            var label = new Label($"  \u2139 {msg.Content}");
            label.style.fontSize     = _theme.FontSize(ThemeContext.FontTier.Hint);
            label.style.color        = _theme.IsDark
                ? new Color(0.45f, 0.45f, 0.5f)
                : new Color(0.5f, 0.5f, 0.55f);
            label.style.marginLeft   = 12;
            label.style.marginBottom = 2;
            InsertBeforeThinking(label);
        }

        /// <summary>Returns the current accumulated streaming content for state persistence.</summary>
        public string GetStreamingContent() => _streamingContent ?? "";

        /// <summary>Restores streaming content after domain reload so previously received tokens are not lost.</summary>
        public void SetStreamingContent(string content) => _streamingContent = content ?? "";

        /// <summary>
        /// Appends a streaming token to the accumulated content and schedules a UI update.
        /// Uses <c>schedule.Execute</c> to defer the update to the UI thread.
        /// </summary>
        /// <param name="token">The incremental text token to append.</param>
        public void AppendStreamingToken(string token)
        {
            _streamingContent += token;
            var snapshot = _streamingContent;
            schedule.Execute(() => _thinkingIndicator.ShowStreaming(snapshot));
        }

        /// <summary>
        /// Hides streaming, commits the accumulated streaming content (or the supplied fallback)
        /// as an Assistant message to the conversation, renders the bubble, resets
        /// <see cref="_streamingContent"/>, and re-shows the thinking indicator for the next turn.
        /// </summary>
        /// <param name="text">Authoritative text from the sidecar (used as fallback if no streaming content).</param>
        /// <param name="conversation">The conversation to add the assistant message to.</param>
        public void FinalizeStreamingMessage(string text, Conversation conversation)
        {
            _thinkingIndicator.HideStreaming();

            var content = !string.IsNullOrEmpty(_streamingContent)
                ? _streamingContent.Trim()
                : text?.Trim() ?? "";

            if (!string.IsNullOrEmpty(content))
            {
                conversation.AddMessage(new ChatMessage(MessageRole.Assistant, content));
                AddMessageBubble(conversation.Messages[^1]);
            }

            _streamingContent = "";

            // Re-show thinking bubble for the next tool use / thinking turn
            ShowThinking();
        }

        /// <summary>
        /// Handles the final query result event. Hides thinking and streaming, commits any
        /// remaining streaming content as an assistant message with activity/usage attached,
        /// or (for multi-turn) attaches activity and usage to the last assistant message.
        /// Resets streaming state.
        /// </summary>
        /// <param name="evt">The result SSE event.</param>
        /// <param name="conversation">The conversation to update.</param>
        /// <param name="currentActivity">The activity log accumulated during this generation turn.</param>
        public void HandleQueryResult(SidecarEvent evt, Conversation conversation, ActivityLog currentActivity)
        {
            HideThinking();
            _thinkingIndicator.HideStreaming();

            var content = _streamingContent.Trim();

            if (!string.IsNullOrEmpty(content))
            {
                conversation.AddMessage(new ChatMessage(MessageRole.Assistant, content));
                conversation.Messages[^1].Activity = currentActivity;
                AddMessageBubble(conversation.Messages[^1]);

                var usage = new TokenUsage
                {
                    InputTokens  = evt.InputTokens,
                    OutputTokens = evt.OutputTokens,
                };
                AddUsageLabel(usage);
            }
            else if (conversation.Messages.Count > 0
                && conversation.Messages[^1].Role == MessageRole.Assistant)
            {
                // Multi-turn: text already committed by FinalizeStreamingMessage.
                // Still attach activity and usage to the last assistant message.
                conversation.Messages[^1].Activity ??= currentActivity;

                if (currentActivity?.HasEntries == true)
                    AddPersistedActivityBubble(currentActivity);

                var usage = new TokenUsage
                {
                    InputTokens  = evt.InputTokens,
                    OutputTokens = evt.OutputTokens,
                };
                AddUsageLabel(usage);
            }

            _streamingContent = "";
        }

        /// <summary>
        /// Hides thinking and streaming, adds an error system message to the conversation,
        /// renders the bubble, and resets streaming state.
        /// </summary>
        /// <param name="message">The error message text.</param>
        /// <param name="conversation">The conversation to add the error message to.</param>
        public void HandleQueryError(string message, Conversation conversation)
        {
            HideThinking();
            _thinkingIndicator.HideStreaming();

            var errorMsg = $"Error: {message}";
            conversation.AddMessage(new ChatMessage(MessageRole.System, errorMsg));
            AddMessageBubble(conversation.Messages[^1]);

            _streamingContent = "";
        }

        /// <summary>
        /// Shows the thinking indicator.
        /// </summary>
        public void ShowThinking()
        {
            _thinkingIndicator.Show();
        }

        /// <summary>
        /// Hides the thinking indicator.
        /// </summary>
        public void HideThinking()
        {
            _thinkingIndicator.Hide();
        }

        /// <summary>
        /// Updates the stream phase shown in the thinking indicator.
        /// </summary>
        /// <param name="phase">The new stream phase.</param>
        /// <param name="toolName">The tool name for <see cref="StreamPhase.ToolUse"/> phases.</param>
        public void UpdatePhase(StreamPhase phase, string toolName)
        {
            _thinkingIndicator.UpdatePhase(phase, toolName);
        }

        /// <summary>
        /// Handles a permission request SSE event. If the tool is "AskUserQuestion", shows
        /// a question prompt with a text input and Send/Skip buttons. Otherwise creates a
        /// <see cref="PermissionPromptElement"/> with Allow/Deny/Abort controls.
        /// </summary>
        /// <param name="evt">The permission_request SSE event.</param>
        /// <param name="client">The sidecar client used to approve/deny the request.</param>
        public void HandlePermissionRequest(SidecarEvent evt, SidecarClient client)
        {
            if (evt.Tool == "AskUserQuestion")
            {
                ShowUserQuestionPrompt(evt, client);
                return;
            }

            var prompt = new PermissionPromptElement(evt.Id, evt.Tool, evt.Input, _theme.IsDark);

            prompt.OnDecision += async (id, type) =>
            {
                if (type == "deny")
                    await client.Deny(id);
                else
                    await client.Approve(id, type == "allow_session");
            };

            prompt.OnAbort += async () =>
            {
                await client.Cancel();
                OnAbortRequested?.Invoke();
            };

            InsertBeforeThinking(prompt);
            ScrollToBottom();
        }

        /// <summary>
        /// Handles a tool_activity SSE event. Updates the activity log, surfaces plan steps
        /// for TodoWrite/TaskCreate tools, and updates the thinking indicator.
        /// </summary>
        /// <param name="evt">The tool_activity SSE event.</param>
        /// <param name="currentActivity">The activity log for the current generation turn.</param>
        public void HandleToolActivity(SidecarEvent evt, ActivityLog currentActivity)
        {
            if (currentActivity == null) return;

            var toolName = evt.ToolName ?? evt.Tool;
            currentActivity.HandleToolActivity(
                evt.ToolUseId, toolName, evt.InputJson, evt.ParentTaskId);

            if (toolName == "TodoWrite" || toolName == "TaskCreate")
                SurfacePlanSteps(toolName, evt.InputJson);

            _thinkingIndicator.SetActivity(currentActivity);
            _thinkingIndicator.UpdateActivityDisplay();
        }

        /// <summary>
        /// Handles a task SSE event. Updates the activity log and the thinking indicator.
        /// </summary>
        /// <param name="evt">The task SSE event.</param>
        /// <param name="currentActivity">The activity log for the current generation turn.</param>
        public void HandleTaskEvent(SidecarEvent evt, ActivityLog currentActivity)
        {
            if (currentActivity == null) return;

            currentActivity.HandleTaskEvent(evt.TaskId, evt.Status, evt.Description, evt.Error);

            _thinkingIndicator.SetActivity(currentActivity);
            _thinkingIndicator.UpdateActivityDisplay();
        }

        /// <summary>
        /// Handles tool_progress SSE events by updating the thinking indicator
        /// with the currently running tool name and its elapsed time.
        /// </summary>
        /// <param name="evt">The tool_progress SSE event.</param>
        public void HandleToolProgress(SidecarEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.ToolName))
                _thinkingIndicator.UpdateToolProgress(evt.ToolName, evt.ElapsedSeconds);
        }

        /// <summary>
        /// Hides the thinking and streaming indicators and resets streaming content.
        /// </summary>
        public void OnGenerationComplete()
        {
            HideThinking();
            _thinkingIndicator.HideStreaming();
            _streamingContent = "";
        }

        /// <summary>
        /// Clears the scroll view and rebuilds it from the conversation's message history.
        /// Recreates the welcome panel and thinking indicator. Scrolls to bottom when done.
        /// </summary>
        /// <param name="conversation">The conversation whose messages to render.</param>
        public void RebuildMessages(Conversation conversation)
        {
            _scrollView.Clear();

            BuildWelcomePanel();
            _welcomePanel.style.display = conversation.Messages.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            foreach (var msg in conversation.Messages)
            {
                switch (msg.Role)
                {
                    case MessageRole.ToolCall:
                        AddToolCallBubble(msg);
                        break;
                    case MessageRole.ProjectContext:
                        AddProjectContextBubble(msg);
                        break;
                    case MessageRole.Info:
                        AddInfoBubble(msg);
                        break;
                    case MessageRole.TokenUsage:
                        AddTokenUsageBubble(msg);
                        break;
                    default:
                        AddMessageBubble(msg);
                        break;
                }
            }

            // Re-create the thinking indicator
            _thinkingIndicator = new ThinkingIndicator(_theme);
            _thinkingIndicator.OnScrollRequested += ScrollToBottom;
            _scrollView.Add(_thinkingIndicator);

            ScrollToBottom();
        }

        /// <summary>
        /// Enables auto-scroll and schedules a scroll to the bottom of the view.
        /// </summary>
        public void ScrollToBottom()
        {
            _autoScroll = true;
            _scrollView?.schedule.Execute(() =>
            {
                _scrollView.scrollOffset = new Vector2(0, float.MaxValue);
            });
        }

        // ── Private methods ──────────────────────────────────────────────────

        /// <summary>
        /// Creates the scroll view, registers geometry/scroll callbacks, builds the welcome
        /// panel, and creates the initial thinking indicator.
        /// </summary>
        void Build()
        {
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow     = 1;
            _scrollView.style.paddingTop   = 8;
            _scrollView.style.paddingBottom = 8;
            _scrollView.style.paddingLeft  = 8;
            _scrollView.style.paddingRight = 8;

            _scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(
                _ => OnScrollContentChanged());
            _scrollView.verticalScroller.valueChanged += _ => OnUserScrolled();

            Add(_scrollView);

            BuildWelcomePanel();

            _thinkingIndicator = new ThinkingIndicator(_theme);
            _thinkingIndicator.OnScrollRequested += ScrollToBottom;
            _scrollView.Add(_thinkingIndicator);
        }

        /// <summary>
        /// Builds and adds the welcome panel to the scroll view.
        /// Shows title, subtitle, key commands, and clickable starter suggestions.
        /// </summary>
        void BuildWelcomePanel()
        {
            _welcomePanel = new VisualElement();
            _welcomePanel.style.flexGrow      = 1;
            _welcomePanel.style.justifyContent = Justify.Center;
            _welcomePanel.style.alignItems    = Align.Center;
            _welcomePanel.style.paddingTop    = 40;

            var title = new Label("UniClaude");
            title.style.fontSize                  = _theme.FontSize(ThemeContext.FontTier.Title);
            title.style.unityFontStyleAndWeight   = FontStyle.Bold;
            title.style.unityTextAlign            = TextAnchor.MiddleCenter;
            title.style.marginBottom              = 8;
            _welcomePanel.Add(title);

            var subtitle = new Label("AI assistant for Unity");
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.whiteSpace     = WhiteSpace.Normal;
            subtitle.style.marginBottom   = 20;
            subtitle.style.fontSize       = _theme.FontSize(ThemeContext.FontTier.Body);
            _welcomePanel.Add(subtitle);

            // Key commands
            var commandsLabel = new Label(
                "/help  \u00b7  /model  \u00b7  /effort  \u00b7  /plan  \u00b7  /undo");
            commandsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            commandsLabel.style.fontSize       = _theme.FontSize(ThemeContext.FontTier.Meta);
            commandsLabel.style.color          = _theme.DimText;
            commandsLabel.style.marginBottom   = 24;
            _welcomePanel.Add(commandsLabel);

            // Starter suggestions
            var starters = new[]
            {
                "Create a player controller script",
                "Explain the scene hierarchy",
                "Add a health bar UI to the HUD",
                "Set up a basic lighting rig"
            };

            var starterContainer = new VisualElement();
            starterContainer.style.alignItems = Align.Center;
            starterContainer.style.maxWidth   = 340;

            foreach (var starter in starters)
            {
                var btn = new Button(() => OnStarterClicked?.Invoke(starter));
                btn.text = starter;
                btn.style.fontSize          = _theme.FontSize(ThemeContext.FontTier.Meta);
                btn.style.marginBottom      = 6;
                btn.style.paddingLeft       = 16;
                btn.style.paddingRight      = 16;
                btn.style.paddingTop        = 6;
                btn.style.paddingBottom     = 6;
                btn.style.borderTopLeftRadius     = 12;
                btn.style.borderTopRightRadius    = 12;
                btn.style.borderBottomLeftRadius   = 12;
                btn.style.borderBottomRightRadius  = 12;
                btn.style.unityTextAlign    = TextAnchor.MiddleCenter;
                btn.style.alignSelf         = Align.Stretch;
                starterContainer.Add(btn);
            }

            _welcomePanel.Add(starterContainer);

            var hint = new Label("Escape to cancel  \u00b7  Shift+Enter for newline");
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.fontSize       = _theme.FontSize(ThemeContext.FontTier.Hint);
            hint.style.color          = _theme.DimText;
            hint.style.marginTop      = 20;
            _welcomePanel.Add(hint);

            _scrollView.Add(_welcomePanel);
        }

        /// <summary>
        /// Inserts an element into the scroll view immediately before the
        /// <see cref="ThinkingIndicator"/>. Falls back to appending at the end if the
        /// indicator is not present in the scroll view.
        /// </summary>
        /// <param name="element">The element to insert.</param>
        void InsertBeforeThinking(VisualElement element)
        {
            int insertIndex = _scrollView.childCount;
            if (_thinkingIndicator?.parent == _scrollView)
                insertIndex = _scrollView.IndexOf(_thinkingIndicator);
            _scrollView.Insert(insertIndex, element);
        }

        /// <summary>
        /// Renders a collapsible activity summary bubble for a completed assistant turn.
        /// Clicking the header expands or collapses the static activity tree.
        /// </summary>
        /// <param name="activity">The activity log to summarise.</param>
        void AddPersistedActivityBubble(ActivityLog activity)
        {
            var totalCount = activity.TotalToolCount;
            if (totalCount == 0) return;

            var container = new VisualElement();
            container.style.marginLeft   = 12;
            container.style.marginRight  = 12;
            container.style.marginBottom = 4;

            var header = new Button
            {
                text = $"\u25b6 {totalCount} tool use{(totalCount != 1 ? "s" : "")} (click to expand)"
            };
            header.style.flexGrow          = 1;
            header.style.backgroundColor   = Color.clear;
            header.style.borderTopWidth    = 0;
            header.style.borderBottomWidth = 0;
            header.style.borderLeftWidth   = 0;
            header.style.borderRightWidth  = 0;
            header.style.color             = _theme.IsDark
                ? new Color(0.45f, 0.45f, 0.50f)
                : new Color(0.4f, 0.4f, 0.45f);
            header.style.fontSize          = _theme.FontSize(ThemeContext.FontTier.Hint);
            header.style.unityTextAlign    = TextAnchor.MiddleLeft;
            header.style.paddingLeft       = 4;
            header.style.height            = 20;

            var detail = BuildStaticActivityTree(activity);
            detail.style.display = DisplayStyle.None;

            bool expanded = false;
            header.clicked += () =>
            {
                expanded = !expanded;
                detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                header.text = (expanded ? "\u25bc" : "\u25b6")
                    + $" {totalCount} tool use{(totalCount != 1 ? "s" : "")} (click to {(expanded ? "collapse" : "expand")})";
            };

            container.Add(header);
            container.Add(detail);
            InsertBeforeThinking(container);
        }

        /// <summary>
        /// Creates a container with an activity tree populated from the given log.
        /// </summary>
        /// <param name="activity">The activity log to visualise.</param>
        /// <returns>A <see cref="VisualElement"/> containing the activity tree rows.</returns>
        VisualElement BuildStaticActivityTree(ActivityLog activity)
        {
            var tree = new VisualElement();
            tree.style.marginLeft   = 8;
            tree.style.paddingTop   = 4;
            tree.style.paddingBottom = 4;
            PopulateStaticActivityTree(tree, activity);
            return tree;
        }

        /// <summary>
        /// Iterates <paramref name="activity"/>'s entries and adds a label for each tool
        /// invocation or subagent task (with nested tools) to <paramref name="container"/>.
        /// Uses dim/bright colors and Hint font, matching the non-live persisted display.
        /// </summary>
        /// <param name="container">The element that receives the generated tree rows.</param>
        /// <param name="activity">The activity log to visualise.</param>
        void PopulateStaticActivityTree(VisualElement container, ActivityLog activity)
        {
            var dimColor = _theme.IsDark
                ? new Color(0.50f, 0.50f, 0.55f)
                : new Color(0.40f, 0.40f, 0.45f);

            var brightColor = _theme.IsDark
                ? new Color(0.65f, 0.65f, 0.70f)
                : new Color(0.30f, 0.30f, 0.35f);

            int fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);

            foreach (var entry in activity.OrderedEntries)
            {
                if (entry is ToolActivity tool)
                {
                    var label = new Label(
                        $"\u251c {ActivityLog.FormatToolLabel(tool.ToolName, tool.InputJson)}");
                    label.style.color       = dimColor;
                    label.style.fontSize    = fontSize;
                    label.style.marginBottom = 1;
                    container.Add(label);
                }
                else if (entry is TaskActivity task)
                {
                    var statusIcon = task.Status == "completed" ? "\u2713" : "\u25b8";
                    var taskLabel  = new Label($"{statusIcon} {task.Description}");
                    taskLabel.style.color                = brightColor;
                    taskLabel.style.fontSize             = fontSize;
                    taskLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    taskLabel.style.marginBottom         = 1;
                    container.Add(taskLabel);

                    foreach (var child in task.Children)
                    {
                        var childLabel = new Label(
                            $"\u2502  \u251c {ActivityLog.FormatToolLabel(child.ToolName, child.InputJson)}");
                        childLabel.style.color       = dimColor;
                        childLabel.style.fontSize    = fontSize;
                        childLabel.style.marginBottom = 1;
                        container.Add(childLabel);
                    }
                }
            }
        }

        /// <summary>
        /// Parses TodoWrite/TaskCreate tool inputs and renders them as expandable plan step
        /// bubbles inserted before the thinking indicator.
        /// </summary>
        /// <param name="toolName">Either "TodoWrite" or "TaskCreate".</param>
        /// <param name="inputJson">Raw JSON string of the tool input.</param>
        void SurfacePlanSteps(string toolName, string inputJson)
        {
            if (string.IsNullOrEmpty(inputJson)) return;

            try
            {
                var obj = JObject.Parse(inputJson);

                var container = new VisualElement();
                container.style.marginLeft   = 12;
                container.style.marginRight  = 12;
                container.style.marginTop    = 4;
                container.style.marginBottom = 4;
                container.style.paddingTop   = 8;
                container.style.paddingBottom = 8;
                container.style.paddingLeft  = 10;
                container.style.paddingRight = 10;
                container.style.borderTopLeftRadius     = 6;
                container.style.borderTopRightRadius    = 6;
                container.style.borderBottomLeftRadius  = 6;
                container.style.borderBottomRightRadius = 6;
                container.style.backgroundColor = _theme.IsDark
                    ? new Color(0.15f, 0.17f, 0.20f)
                    : new Color(0.94f, 0.94f, 0.96f);

                var dimColor  = _theme.IsDark
                    ? new Color(0.55f, 0.55f, 0.6f)
                    : new Color(0.4f, 0.4f, 0.45f);
                var textColor = _theme.IsDark
                    ? new Color(0.8f, 0.8f, 0.82f)
                    : new Color(0.2f, 0.2f, 0.22f);

                if (toolName == "TodoWrite")
                {
                    var header = new Label("\u2611 Plan Steps");
                    header.style.fontSize               = _theme.FontSize(ThemeContext.FontTier.Meta);
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    header.style.color                  = dimColor;
                    header.style.marginBottom           = 4;
                    container.Add(header);

                    if (obj["todos"] is JArray todos)
                    {
                        foreach (var todo in todos)
                        {
                            var status  = todo["status"]?.ToString() ?? "pending";
                            var content = todo["content"]?.ToString() ?? "";
                            var icon    = status switch
                            {
                                "completed"   => "\u2713",
                                "in_progress" => "\u25b6",
                                _             => "\u25cb",
                            };
                            var line = new Label($"  {icon}  {content}");
                            line.style.fontSize    = _theme.FontSize(ThemeContext.FontTier.Body);
                            line.style.color       = textColor;
                            line.style.whiteSpace  = WhiteSpace.Normal;
                            line.style.marginBottom = 2;
                            container.Add(line);
                        }
                    }
                }
                else if (toolName == "TaskCreate")
                {
                    var subject = obj["subject"]?.ToString() ?? obj["title"]?.ToString() ?? "";
                    var description = obj["description"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(subject)) return;

                    var line = new Label($"\u25cb  {subject}");
                    line.style.fontSize   = _theme.FontSize(ThemeContext.FontTier.Body);
                    line.style.color      = textColor;
                    line.style.whiteSpace = WhiteSpace.Normal;
                    container.Add(line);

                    if (!string.IsNullOrEmpty(description))
                    {
                        var desc = new Label($"     {description}");
                        desc.style.fontSize   = _theme.FontSize(ThemeContext.FontTier.Meta);
                        desc.style.color      = dimColor;
                        desc.style.whiteSpace = WhiteSpace.Normal;
                        container.Add(desc);
                    }
                }

                if (container.childCount > 0)
                {
                    InsertBeforeThinking(container);
                    ScrollToBottom();
                }
            }
            catch
            {
                // Malformed input — skip rendering
            }
        }

        /// <summary>
        /// Shows a question prompt UI for an AskUserQuestion permission event.
        /// Includes styled container, question text, a multiline answer field, and Send/Skip buttons.
        /// </summary>
        /// <param name="evt">The AskUserQuestion permission_request event.</param>
        /// <param name="client">The sidecar client used to submit the answer.</param>
        void ShowUserQuestionPrompt(SidecarEvent evt, SidecarClient client)
        {
            var questionText = "Claude has a question for you";
            if (evt.Input != null)
            {
                var questions = evt.Input["questions"];
                if (questions is JArray arr && arr.Count > 0)
                {
                    var first = arr[0];
                    questionText = first["question"]?.ToString() ?? questionText;
                }
                else
                {
                    questionText = evt.Input["question"]?.ToString() ?? questionText;
                }
            }

            var container = new VisualElement();
            container.style.marginLeft   = 16;
            container.style.marginRight  = 16;
            container.style.marginTop    = 4;
            container.style.marginBottom = 4;
            container.style.paddingTop   = 10;
            container.style.paddingBottom = 10;
            container.style.paddingLeft  = 12;
            container.style.paddingRight = 12;
            container.style.borderTopLeftRadius     = 6;
            container.style.borderTopRightRadius    = 6;
            container.style.borderBottomLeftRadius  = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.borderTopWidth    = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth   = 1;
            container.style.borderRightWidth  = 1;

            var borderColor = _theme.IsDark
                ? new Color(0.3f, 0.35f, 0.45f)
                : new Color(0.6f, 0.65f, 0.8f);
            container.style.borderTopColor    = borderColor;
            container.style.borderBottomColor = borderColor;
            container.style.borderLeftColor   = borderColor;
            container.style.borderRightColor  = borderColor;
            container.style.backgroundColor = _theme.IsDark
                ? new Color(0.16f, 0.18f, 0.24f)
                : new Color(0.93f, 0.94f, 0.98f);

            // Header
            var header = new Label("\u2753 Claude is asking");
            header.style.fontSize               = _theme.FontSize(ThemeContext.FontTier.Body);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom           = 6;
            header.style.color = _theme.IsDark
                ? new Color(0.85f, 0.85f, 0.9f)
                : new Color(0.2f, 0.2f, 0.25f);
            container.Add(header);

            // Question text
            var questionLabel = new Label(questionText);
            questionLabel.style.fontSize    = _theme.FontSize(ThemeContext.FontTier.Body);
            questionLabel.style.whiteSpace  = WhiteSpace.Normal;
            questionLabel.style.marginBottom = 8;
            questionLabel.style.color = _theme.IsDark
                ? new Color(0.75f, 0.75f, 0.8f)
                : new Color(0.25f, 0.25f, 0.3f);
            container.Add(questionLabel);

            // Answer text field
            var answerField = new TextField { multiline = true };
            answerField.style.minHeight    = 40;
            answerField.style.marginBottom = 10;
            answerField.style.whiteSpace   = WhiteSpace.Normal;
            answerField.style.fontSize     = _theme.FontSize(ThemeContext.FontTier.Body);
            container.Add(answerField);

            // Button row
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;

            var submitted = false;

            var sendBtn = new Button(() =>
            {
                if (submitted) return;
                submitted = true;
                var answer = answerField.value?.Trim() ?? "";
                container.SetEnabled(false);
                container.style.opacity = 0.6f;
                _ = client.Approve(evt.Id, false, answer);
            })
            { text = "Send" };
            sendBtn.style.fontSize              = _theme.FontSize(ThemeContext.FontTier.Body);
            sendBtn.style.height                = 28;
            sendBtn.style.paddingLeft           = 14;
            sendBtn.style.paddingRight          = 14;
            sendBtn.style.borderTopLeftRadius   = 4;
            sendBtn.style.borderTopRightRadius  = 4;
            sendBtn.style.borderBottomLeftRadius  = 4;
            sendBtn.style.borderBottomRightRadius = 4;
            sendBtn.style.backgroundColor = _theme.IsDark
                ? new Color(0.2f, 0.35f, 0.55f)
                : new Color(0.25f, 0.47f, 0.85f);
            sendBtn.style.color            = Color.white;
            sendBtn.style.borderTopWidth   = 0;
            sendBtn.style.borderBottomWidth = 0;
            sendBtn.style.borderLeftWidth  = 0;
            sendBtn.style.borderRightWidth = 0;
            buttonRow.Add(sendBtn);

            var skipBtn = new Button(() =>
            {
                if (submitted) return;
                submitted = true;
                container.SetEnabled(false);
                container.style.opacity = 0.6f;
                _ = client.Deny(evt.Id);
            })
            { text = "Skip" };
            skipBtn.style.fontSize              = _theme.FontSize(ThemeContext.FontTier.Body);
            skipBtn.style.height                = 28;
            skipBtn.style.paddingLeft           = 12;
            skipBtn.style.paddingRight          = 12;
            skipBtn.style.marginLeft            = 6;
            skipBtn.style.backgroundColor       = Color.clear;
            skipBtn.style.borderTopWidth        = 0;
            skipBtn.style.borderBottomWidth     = 0;
            skipBtn.style.borderLeftWidth       = 0;
            skipBtn.style.borderRightWidth      = 0;
            skipBtn.style.color = _theme.IsDark
                ? new Color(0.55f, 0.55f, 0.6f)
                : new Color(0.45f, 0.45f, 0.5f);
            buttonRow.Add(skipBtn);

            container.Add(buttonRow);
            InsertBeforeThinking(container);
            ScrollToBottom();

            // Focus the answer field after a short delay
            answerField.schedule.Execute(() => answerField.Focus()).StartingIn(50);
        }

        // ── Scroll management ────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the scroll view is at or near the bottom.
        /// </summary>
        bool IsScrolledToBottom()
        {
            var scroller = _scrollView.verticalScroller;
            return scroller.highValue <= 0 || scroller.value >= scroller.highValue - 20;
        }

        /// <summary>
        /// Called when the user interacts with the scroll bar.
        /// Disables auto-scroll when they scroll up; re-enables when they reach the bottom.
        /// </summary>
        void OnUserScrolled()
        {
            _autoScroll = IsScrolledToBottom();
        }

        /// <summary>
        /// Called when the scroll content geometry changes (new children, layout shift).
        /// Auto-scrolls to the bottom if the user has not scrolled away.
        /// </summary>
        void OnScrollContentChanged()
        {
            if (_autoScroll)
                _scrollView.scrollOffset = new Vector2(0, float.MaxValue);
        }
    }
}
