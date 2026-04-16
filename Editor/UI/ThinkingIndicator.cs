using System;
using UniClaude.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// A <see cref="VisualElement"/> that encapsulates the thinking animation (spinner +
    /// timer), the activity tree display, and the streaming response bubble shown while
    /// Claude is generating a response.
    /// </summary>
    public class ThinkingIndicator : VisualElement
    {
        // ── Spinner ──────────────────────────────────────────────────────────

        static readonly string[] SpinnerFrames = { "/", "\u2014", "\\", "|" };

        // ── Fields ───────────────────────────────────────────────────────────

        readonly ThemeContext _theme;

        // Thinking bubble elements
        VisualElement _thinkingBubble;
        Label         _spinnerLabel;
        Label         _thinkingLabel;
        Label         _timerLabel;
        Label         _activityCountLabel;
        VisualElement _activityContainer;

        // Streaming bubble elements
        VisualElement _streamingBubble;
        Label         _streamingLabel;

        // State
        IVisualElementScheduledItem _thinkingSchedule;
        double                       _thinkingStartTime;
        string                       _thinkingPhaseText = "Thinking";
        ActivityLog                  _currentActivity;
        bool                         _activityExpanded;

        // ── Event ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when content changes require the parent scroll view to jump to the bottom
        /// (e.g. after <see cref="Show"/> or after <see cref="UpdateActivityDisplay"/>).
        /// </summary>
        public event Action OnScrollRequested;

        // ── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds the thinking bubble and streaming bubble using the supplied theme.
        /// </summary>
        /// <param name="theme">Theme context supplying colors and font sizes.</param>
        public ThinkingIndicator(ThemeContext theme)
        {
            _theme = theme;

            BuildThinkingBubble();
            BuildStreamingBubble();

            Add(_thinkingBubble);
            Add(_streamingBubble);
        }

        // ── Build helpers ────────────────────────────────────────────────────

        void BuildThinkingBubble()
        {
            _thinkingBubble = MessageRenderer.CreateBubble(_theme, MessageRole.Assistant);
            _thinkingBubble.style.display = DisplayStyle.None;

            _thinkingBubble.Add(MessageRenderer.MakeRoleLabel(_theme, "Claude"));

            // Horizontal row: spinner | label | timer
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var spinnerColor = new Color(0.55f, 0.55f, 0.65f);

            _spinnerLabel = new Label(SpinnerFrames[0]);
            _spinnerLabel.style.width = 14;
            _spinnerLabel.style.color = spinnerColor;
            _spinnerLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            _spinnerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            _thinkingLabel = new Label("Thinking...");
            _thinkingLabel.style.color = spinnerColor;
            _thinkingLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);
            _thinkingLabel.style.marginLeft = 4;

            _timerLabel = new Label("0s");
            _timerLabel.style.color = new Color(0.45f, 0.45f, 0.52f);
            _timerLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Meta);
            _timerLabel.style.marginLeft = 8;

            row.Add(_spinnerLabel);
            row.Add(_thinkingLabel);
            row.Add(_timerLabel);

            _thinkingBubble.Add(row);

            // Activity count label (collapsible toggle)
            _activityCountLabel = new Label();
            _activityCountLabel.style.color = new Color(0.45f, 0.45f, 0.50f);
            _activityCountLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Hint);
            _activityCountLabel.style.marginLeft = 18;
            _activityCountLabel.style.marginTop = 2;
            _activityCountLabel.style.display = DisplayStyle.None;
            _activityCountLabel.AddToClassList("unity-cursor-link");
            _activityCountLabel.RegisterCallback<ClickEvent>(_ => ToggleActivityExpanded());

            _thinkingBubble.Add(_activityCountLabel);

            // Activity tree container
            _activityContainer = new VisualElement();
            _activityContainer.style.marginLeft = 8;
            _activityContainer.style.marginTop = 4;
            _activityContainer.style.display = DisplayStyle.None;

            _thinkingBubble.Add(_activityContainer);
        }

        void BuildStreamingBubble()
        {
            _streamingBubble = MessageRenderer.CreateBubble(_theme, MessageRole.Assistant);
            _streamingBubble.style.display = DisplayStyle.None;

            _streamingBubble.Add(MessageRenderer.MakeRoleLabel(_theme, "Claude"));

            _streamingLabel = new Label();
            _streamingLabel.style.whiteSpace = WhiteSpace.Normal;
            _streamingLabel.style.color = _theme.TextColor;
            _streamingLabel.style.fontSize = _theme.FontSize(ThemeContext.FontTier.Body);

            _streamingBubble.Add(_streamingLabel);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Shows the thinking bubble and starts the spinner/timer animation.
        /// Resets the phase to "Thinking" and clears any previous activity display.
        /// </summary>
        public void Show()
        {
            _thinkingPhaseText = "Thinking";
            _thinkingBubble.style.display = DisplayStyle.Flex;
            _thinkingStartTime = EditorApplication.timeSinceStartup;

            _activityContainer.Clear();
            _activityContainer.style.display = DisplayStyle.None;
            _activityCountLabel.style.display = DisplayStyle.None;

            _thinkingSchedule?.Pause();
            _thinkingSchedule = _thinkingBubble.schedule.Execute(() =>
            {
                double elapsed = EditorApplication.timeSinceStartup - _thinkingStartTime;
                int frameIndex = (int)(elapsed * 8) % SpinnerFrames.Length;
                _spinnerLabel.text = SpinnerFrames[frameIndex];
                _thinkingLabel.text = $"{_thinkingPhaseText}...";
                _timerLabel.text = MessageRenderer.FormatElapsed(elapsed);
            }).Every(100);

            OnScrollRequested?.Invoke();
        }

        /// <summary>
        /// Pauses the spinner animation and hides the thinking bubble.
        /// Resets the phase text to "Thinking".
        /// </summary>
        public void Hide()
        {
            _thinkingSchedule?.Pause();
            _thinkingBubble.style.display = DisplayStyle.None;
            _thinkingPhaseText = "Thinking";
        }

        /// <summary>
        /// Updates the phase text shown alongside the spinner based on the current
        /// streaming phase.
        /// </summary>
        /// <param name="phase">The new stream phase.</param>
        /// <param name="toolName">
        /// The name of the tool being used, relevant when <paramref name="phase"/> is
        /// <see cref="StreamPhase.ToolUse"/>.
        /// </param>
        public void UpdatePhase(StreamPhase phase, string toolName)
        {
            _thinkingPhaseText = phase switch
            {
                StreamPhase.Writing => "Writing",
                StreamPhase.ToolUse => !string.IsNullOrEmpty(toolName)
                    ? $"Using {toolName}"
                    : "Using tool",
                _ => "Thinking",
            };
        }

        /// <summary>
        /// Shows the streaming bubble and sets its text to <paramref name="content"/>
        /// followed by a block cursor character.
        /// </summary>
        /// <param name="content">Partial response text to display.</param>
        public void ShowStreaming(string content)
        {
            _streamingBubble.style.display = DisplayStyle.Flex;
            _streamingLabel.text = content + " \u258c";
        }

        /// <summary>
        /// Hides the streaming bubble and clears its label text.
        /// </summary>
        public void HideStreaming()
        {
            _streamingBubble.style.display = DisplayStyle.None;
            _streamingLabel.text = string.Empty;
        }

        /// <summary>
        /// Updates the phase text with per-tool progress from a tool_progress event.
        /// Shows the tool name and its elapsed time.
        /// </summary>
        /// <param name="toolName">The name of the tool currently executing.</param>
        /// <param name="elapsedSeconds">Seconds elapsed since the tool started.</param>
        public void UpdateToolProgress(string toolName, float elapsedSeconds)
        {
            _thinkingPhaseText = elapsedSeconds >= 1f
                ? $"{toolName} ({elapsedSeconds:F0}s)"
                : toolName;
        }

        /// <summary>
        /// Sets the activity log that drives the activity tree display.
        /// </summary>
        /// <param name="activity">The current <see cref="ActivityLog"/> for this turn.</param>
        public void SetActivity(ActivityLog activity)
        {
            _currentActivity = activity;
        }

        /// <summary>
        /// Updates the activity counter label and phase text from the current activity log.
        /// Shows the count label when more than one tool has been invoked, and rebuilds
        /// the activity tree if it is currently expanded.
        /// </summary>
        public void UpdateActivityDisplay()
        {
            if (_currentActivity == null || !_currentActivity.HasEntries)
                return;

            var latest = _currentActivity.LatestTool;
            if (latest != null)
                _thinkingPhaseText = ActivityLog.FormatToolLabel(latest.ToolName, latest.InputJson);

            var totalCount = _currentActivity.TotalToolCount;
            if (totalCount > 1)
            {
                var extraCount = totalCount - 1;
                _activityCountLabel.text = _activityExpanded
                    ? "(click to collapse)"
                    : $"  +{extraCount} more tool use{(extraCount != 1 ? "s" : "")} (click to expand)";
                _activityCountLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _activityCountLabel.style.display = DisplayStyle.None;
            }

            if (_activityExpanded)
                RebuildActivityTree();

            OnScrollRequested?.Invoke();
        }

        /// <summary>
        /// Rebuilds all internal elements to reflect font-size changes.
        /// Clears both bubbles and recreates them from scratch.
        /// </summary>
        public void Rebuild()
        {
            _thinkingSchedule?.Pause();
            _thinkingSchedule = null;

            Clear();

            BuildThinkingBubble();
            BuildStreamingBubble();

            Add(_thinkingBubble);
            Add(_streamingBubble);
        }

        // ── Private activity tree ────────────────────────────────────────────

        /// <summary>
        /// Toggles the activity tree between expanded and collapsed, rebuilding or
        /// updating the display accordingly.
        /// </summary>
        void ToggleActivityExpanded()
        {
            _activityExpanded = !_activityExpanded;

            if (_activityExpanded)
            {
                _activityContainer.style.display = DisplayStyle.Flex;
                RebuildActivityTree();
            }
            else
            {
                _activityContainer.style.display = DisplayStyle.None;
            }

            // Refresh count label text to reflect new collapse/expand state
            UpdateActivityDisplay();
        }

        /// <summary>
        /// Clears the activity container and repopulates it from the current activity log.
        /// </summary>
        void RebuildActivityTree()
        {
            _activityContainer.Clear();
            if (_currentActivity != null)
                PopulateActivityTree(_activityContainer, _currentActivity);
        }

        /// <summary>
        /// Iterates <paramref name="activity"/>'s <see cref="ActivityLog.OrderedEntries"/>
        /// and adds a label for each tool invocation or subagent task (with nested tools)
        /// to <paramref name="container"/>.
        /// </summary>
        /// <param name="container">The element that receives the generated tree rows.</param>
        /// <param name="activity">The activity log to visualize.</param>
        void PopulateActivityTree(VisualElement container, ActivityLog activity)
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
                    var label = new Label($"\u251c {ActivityLog.FormatToolLabel(tool.ToolName, tool.InputJson)}");
                    label.style.color = dimColor;
                    label.style.fontSize = fontSize;
                    label.style.marginBottom = 1;
                    container.Add(label);
                }
                else if (entry is TaskActivity task)
                {
                    var statusIcon = task.Status == "completed" ? "\u2713" : "\u25b8";
                    var taskLabel = new Label($"{statusIcon} {task.Description}");
                    taskLabel.style.color = brightColor;
                    taskLabel.style.fontSize = fontSize;
                    taskLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    taskLabel.style.marginBottom = 1;
                    container.Add(taskLabel);

                    foreach (var child in task.Children)
                    {
                        var childLabel = new Label(
                            $"\u2502  \u251c {ActivityLog.FormatToolLabel(child.ToolName, child.InputJson)}");
                        childLabel.style.color = dimColor;
                        childLabel.style.fontSize = fontSize;
                        childLabel.style.marginBottom = 1;
                        container.Add(childLabel);
                    }
                }
            }
        }
    }
}
