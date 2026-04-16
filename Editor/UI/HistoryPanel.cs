using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// A <see cref="VisualElement"/> that renders the history tab: lists saved conversations
    /// with rename, delete, and "Copy as Markdown" export actions.
    /// </summary>
    public class HistoryPanel : VisualElement
    {
        // ── Fields ───────────────────────────────────────────────────────────

        readonly ThemeContext _theme;
        string _activeConversationId;

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the user clicks a conversation item, passing the conversation ID.
        /// </summary>
        public event Action<string> OnConversationSelected;

        /// <summary>
        /// Fired when the user clicks the "+ New Chat" button.
        /// </summary>
        public event Action OnNewChat;

        /// <summary>
        /// Fired when the user confirms "Clear All" in the confirmation dialog.
        /// </summary>
        public event Action OnClearAll;

        // ── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Initializes a new <see cref="HistoryPanel"/> with the given theme context.
        /// </summary>
        /// <param name="theme">The shared theme context for colors and font sizes.</param>
        public HistoryPanel(ThemeContext theme)
        {
            _theme = theme;
            style.flexGrow = 1;
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the entire history list for the given active conversation.
        /// Clears all existing children and repopulates from <see cref="ConversationStore"/>.
        /// </summary>
        /// <param name="activeConversationId">
        /// The ID of the currently active conversation, used to highlight the active entry.
        /// </param>
        public void Refresh(string activeConversationId)
        {
            _activeConversationId = activeConversationId;
            Clear();

            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;

            var newChatBtn = new Button(() => OnNewChat?.Invoke()) { text = "+ New Chat" };
            newChatBtn.style.height = 28;
            newChatBtn.style.paddingLeft = 12;
            newChatBtn.style.paddingRight = 12;
            header.Add(newChatBtn);

            var clearAllBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                    "Clear All Conversations",
                    "Delete all saved conversations? This cannot be undone.",
                    "Clear All", "Cancel"))
                {
                    ConversationStore.DeleteAll();
                    OnClearAll?.Invoke();
                    Refresh(_activeConversationId);
                }
            }) { text = "Clear All" };
            clearAllBtn.style.height = 28;
            clearAllBtn.style.paddingLeft = 12;
            clearAllBtn.style.paddingRight = 12;
            clearAllBtn.style.marginLeft = 4;
            header.Add(clearAllBtn);

            Add(header);

            // Conversation list
            var index = ConversationStore.LoadIndex();

            if (index.Count == 0)
            {
                var empty = new Label("No saved conversations yet.");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.paddingTop = 40;
                empty.style.color = new Color(0.5f, 0.5f, 0.5f);
                empty.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                Add(empty);
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            foreach (var summary in index)
            {
                scroll.Add(BuildHistoryItem(summary));
            }

            Add(scroll);
        }

        /// <summary>
        /// Builds a single history item row for the given conversation summary.
        /// Includes title, metadata, and hover-revealed action buttons (Rename, Copy as Markdown, Delete).
        /// </summary>
        /// <param name="summary">The conversation summary to render.</param>
        /// <returns>A <see cref="VisualElement"/> representing the history item.</returns>
        public VisualElement BuildHistoryItem(ConversationSummary summary)
        {
            var isActive = summary.Id == _activeConversationId;
            var accentColor = _theme.TabActive;

            var item = new VisualElement();
            item.style.paddingTop = 8;
            item.style.paddingBottom = 8;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.marginLeft = 8;
            item.style.marginRight = 8;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            item.style.borderTopLeftRadius = 6;
            item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = 6;
            item.style.borderBottomRightRadius = 6;
            item.style.borderLeftWidth = 3;
            item.style.borderLeftColor = Color.clear;

            if (isActive)
            {
                item.style.backgroundColor = _theme.AutocompleteSel;
                item.style.borderLeftColor = accentColor;
            }

            // Row: text left, action buttons right
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            item.Add(row);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;
            textCol.style.flexShrink = 1;
            textCol.style.overflow = Overflow.Hidden;
            row.Add(textCol);

            var title = new Label(summary.Title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            title.style.color = _theme.TextColor;
            title.style.overflow = Overflow.Hidden;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.textOverflow = TextOverflow.Ellipsis;
            title.style.marginBottom = 2;
            textCol.Add(title);

            var meta = new Label($"{FormatRelativeTime(summary.UpdatedAt)} \u00b7 {summary.MessageCount} messages");
            meta.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            meta.style.color = _theme.DimText;
            textCol.Add(meta);

            // Action buttons (hidden until hover)
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexShrink = 0;
            actions.style.display = DisplayStyle.None;
            row.Add(actions);

            var capturedId = summary.Id;
            var capturedTitle = summary.Title;

            // Rename button
            var renameBtn = new Button { text = "Rename" };
            renameBtn.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            renameBtn.style.height = 20;
            actions.Add(renameBtn);

            // Copy as Markdown button
            var copyBtn = new Button { text = "Copy as Markdown" };
            copyBtn.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            copyBtn.style.height = 20;
            copyBtn.style.marginLeft = 4;
            actions.Add(copyBtn);

            // Delete button
            var deleteBtn = new Button { text = "Delete" };
            deleteBtn.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            deleteBtn.style.height = 20;
            deleteBtn.style.marginLeft = 4;
            deleteBtn.style.color = new Color(0.9f, 0.4f, 0.4f);
            actions.Add(deleteBtn);

            // ── Rename flow ──────────────────────────────────────────────────

            void StartRename()
            {
                title.style.display = DisplayStyle.None;
                actions.style.display = DisplayStyle.None;

                var field = new TextField { value = capturedTitle };
                field.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
                field.style.marginTop = 0;
                field.style.marginBottom = 0;
                field.style.flexGrow = 1;
                row.Insert(0, field);

                void CommitRename()
                {
                    if (field.parent == null) return;
                    var newTitle = field.value.Trim();
                    field.RemoveFromHierarchy();
                    title.style.display = DisplayStyle.Flex;

                    if (!string.IsNullOrEmpty(newTitle) && newTitle != capturedTitle)
                    {
                        ConversationStore.Rename(capturedId, newTitle);
                        Refresh(_activeConversationId);
                    }
                }

                void CancelRename()
                {
                    field.RemoveFromHierarchy();
                    title.style.display = DisplayStyle.Flex;
                }

                field.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        e.StopPropagation();
                        CommitRename();
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        e.StopPropagation();
                        CancelRename();
                    }
                });

                field.RegisterCallback<FocusOutEvent>(_ => CommitRename());

                field.schedule.Execute(() =>
                {
                    field.Focus();
                    field.SelectAll();
                });
            }

            // ── Delete flow ──────────────────────────────────────────────────

            void DoDelete()
            {
                if (EditorUtility.DisplayDialog(
                    "Delete Conversation",
                    $"Delete \"{capturedTitle}\"?",
                    "Delete", "Cancel"))
                {
                    ConversationStore.Delete(capturedId);
                    Refresh(_activeConversationId);
                }
            }

            // ── Wire up buttons ──────────────────────────────────────────────

            renameBtn.clicked += StartRename;
            deleteBtn.clicked += DoDelete;
            copyBtn.clicked += () => CopyAsMarkdown(capturedId);

            // Click to load conversation
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == renameBtn || evt.target == copyBtn || evt.target == deleteBtn)
                    return;

                OnConversationSelected?.Invoke(capturedId);
            });

            // Right-click context menu
            item.RegisterCallback<ContextClickEvent>(_ =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Rename"), false, StartRename);
                menu.AddItem(new GUIContent("Copy as Markdown"), false, () => CopyAsMarkdown(capturedId));
                menu.AddItem(new GUIContent("Delete"), false, DoDelete);
                menu.ShowAsContext();
            });

            // Hover: highlight + show action buttons
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                actions.style.display = DisplayStyle.Flex;
                item.style.borderLeftColor = accentColor;
                if (!isActive)
                    item.style.backgroundColor = _theme.IsDark
                        ? new Color(0.22f, 0.22f, 0.25f)
                        : new Color(0.92f, 0.92f, 0.94f);
            });

            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                actions.style.display = DisplayStyle.None;
                if (!isActive)
                {
                    item.style.backgroundColor = Color.clear;
                    item.style.borderLeftColor = Color.clear;
                }
            });

            return item;
        }

        /// <summary>
        /// Loads the conversation with the given ID, formats all messages as Markdown,
        /// and copies the result to the system clipboard.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation to export.</param>
        public void CopyAsMarkdown(string conversationId)
        {
            var conversation = ConversationStore.Load(conversationId);
            if (conversation == null)
            {
                Debug.LogWarning($"[UniClaude] Could not load conversation {conversationId} for export.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# {conversation.Title}");
            sb.AppendLine($"*Date: {conversation.CreatedAt}*");
            sb.AppendLine();
            sb.AppendLine("---");

            foreach (var msg in conversation.Messages)
            {
                sb.AppendLine();

                switch (msg.Role)
                {
                    case MessageRole.User:
                        sb.AppendLine($"**You:** {msg.Content}");
                        break;

                    case MessageRole.Assistant:
                        sb.AppendLine($"**Claude:** {msg.Content}");
                        break;

                    case MessageRole.System:
                        sb.AppendLine($"*System: {msg.Content}*");
                        break;

                    case MessageRole.ToolCall:
                        var checkmark = msg.IsError ? "\u2717" : "\u2713";
                        sb.AppendLine($"> {checkmark} **Tool:** {msg.ToolName}");
                        if (!string.IsNullOrEmpty(msg.Content))
                            sb.AppendLine($"> {msg.Content}");
                        break;

                    case MessageRole.TokenUsage:
                        sb.AppendLine($"*Tokens: {msg.InputTokens} in / {msg.OutputTokens} out*");
                        break;

                    case MessageRole.ProjectContext:
                    case MessageRole.Info:
                        // Skip internal metadata entries
                        break;
                }
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("*Exported from UniClaude*");

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[UniClaude] Conversation copied to clipboard ({conversation.Messages.Count} messages).");
        }

        /// <summary>
        /// Formats an ISO 8601 UTC timestamp as a human-readable relative time string.
        /// </summary>
        /// <param name="iso8601">The timestamp string in "o" (round-trip) format.</param>
        /// <returns>
        /// A relative time string such as "just now", "5m ago", "3h ago", "2d ago",
        /// or an abbreviated date like "Apr 3" for older timestamps.
        /// </returns>
        public static string FormatRelativeTime(string iso8601)
        {
            if (!DateTime.TryParse(iso8601, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return "";

            var elapsed = DateTime.UtcNow - dt.ToUniversalTime();

            if (elapsed.TotalMinutes < 1) return "just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
            return dt.ToLocalTime().ToString("MMM d");
        }
    }
}
