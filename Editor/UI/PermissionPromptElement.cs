using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// Inline permission prompt shown when Claude requests a tool call
    /// that hasn't been session-trusted.
    /// </summary>
    public class PermissionPromptElement : VisualElement
    {
        const float TimeoutSeconds = 300f; // 5 minutes

        readonly string _requestId;
        readonly IVisualElementScheduledItem _timeoutHandle;
        bool _decided;

        /// <summary>Fires when user makes a decision. Args: (requestId, decisionType).</summary>
        public event Action<string, string> OnDecision;

        /// <summary>Fires when user clicks Abort (cancel entire generation).</summary>
        public event Action OnAbort;

        /// <summary>
        /// Creates a permission prompt for the given tool call request.
        /// Includes Allow / Always Allow / Deny buttons and an auto-deny timeout (5 minutes).
        /// </summary>
        /// <param name="requestId">Unique identifier for this permission request.</param>
        /// <param name="toolName">Name of the tool requesting permission (e.g. "Bash", "Write").</param>
        /// <param name="input">JSON input parameters for the tool call, used for context display.</param>
        /// <param name="isDark">Whether to use dark theme styling.</param>
        public PermissionPromptElement(string requestId, string toolName, JObject input, bool isDark)
        {
            _requestId = requestId;

            // Container styling
            style.marginLeft = 16;
            style.marginRight = 16;
            style.marginTop = 4;
            style.marginBottom = 4;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 12;
            style.paddingRight = 12;
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            var borderColor = isDark
                ? new Color(0.35f, 0.35f, 0.4f)
                : new Color(0.7f, 0.7f, 0.75f);
            style.borderTopColor = borderColor;
            style.borderBottomColor = borderColor;
            style.borderLeftColor = borderColor;
            style.borderRightColor = borderColor;
            style.backgroundColor = isDark
                ? new Color(0.18f, 0.18f, 0.22f)
                : new Color(0.95f, 0.95f, 0.97f);

            // Header: lock icon + "Permission Request"
            var header = new Label("\U0001f512 Permission Request");
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 6;
            header.style.color = isDark
                ? new Color(0.85f, 0.85f, 0.9f)
                : new Color(0.2f, 0.2f, 0.25f);
            Add(header);

            // Tool name
            var toolLabel = new Label($"Tool: {toolName}");
            toolLabel.style.fontSize = 11;
            toolLabel.style.marginBottom = 2;
            toolLabel.style.color = isDark
                ? new Color(0.7f, 0.7f, 0.75f)
                : new Color(0.35f, 0.35f, 0.4f);
            Add(toolLabel);

            // Context (human-readable)
            var context = FormatToolContext(toolName, input);
            if (!string.IsNullOrEmpty(context))
            {
                var contextLabel = new Label(context);
                contextLabel.style.fontSize = 11;
                contextLabel.style.marginBottom = 8;
                contextLabel.style.whiteSpace = WhiteSpace.Normal;
                contextLabel.style.color = isDark
                    ? new Color(0.6f, 0.6f, 0.65f)
                    : new Color(0.4f, 0.4f, 0.45f);
                Add(contextLabel);
            }

            // Primary button row
            var primaryRow = new VisualElement();
            primaryRow.style.flexDirection = FlexDirection.Row;
            primaryRow.style.marginTop = 4;

            var allowBtn = MakeButton("Allow", isDark, true);
            allowBtn.clicked += () => Decide("allow");
            primaryRow.Add(allowBtn);

            var alwaysBtn = MakeButton("Always Allow", isDark, true);
            alwaysBtn.clicked += () => Decide("allow_session");
            alwaysBtn.style.marginLeft = 6;
            primaryRow.Add(alwaysBtn);

            var denyBtn = MakeButton("Deny", isDark, false);
            denyBtn.clicked += () => Decide("deny");
            denyBtn.style.marginLeft = 6;
            primaryRow.Add(denyBtn);

            Add(primaryRow);

            // Abort row (separate to prevent accidental clicks)
            var abortRow = new VisualElement();
            abortRow.style.flexDirection = FlexDirection.Row;
            abortRow.style.justifyContent = Justify.FlexEnd;
            abortRow.style.marginTop = 6;

            var abortBtn = new Button(() =>
            {
                if (_decided) return;
                _decided = true;
                SetEnabled(false);
                OnAbort?.Invoke();
            })
            { text = "Abort" };
            abortBtn.style.fontSize = 10;
            abortBtn.style.backgroundColor = Color.clear;
            abortBtn.style.borderTopWidth = 0;
            abortBtn.style.borderBottomWidth = 0;
            abortBtn.style.borderLeftWidth = 0;
            abortBtn.style.borderRightWidth = 0;
            abortBtn.style.color = isDark
                ? new Color(0.5f, 0.5f, 0.55f)
                : new Color(0.5f, 0.5f, 0.55f);
            abortRow.Add(abortBtn);

            Add(abortRow);

            // Timeout: auto-deny after 5 minutes
            _timeoutHandle = schedule.Execute(() =>
            {
                if (_decided) return;
                _decided = true;
                SetEnabled(false);
                var timeoutLabel = new Label("Timed out \u2014 denied automatically");
                timeoutLabel.style.fontSize = 10;
                timeoutLabel.style.color = new Color(0.9f, 0.5f, 0.3f);
                timeoutLabel.style.marginTop = 4;
                Add(timeoutLabel);
                OnDecision?.Invoke(_requestId, "deny");
            }).StartingIn((long)(TimeoutSeconds * 1000));
        }

        void Decide(string type)
        {
            if (_decided) return;
            _decided = true;
            _timeoutHandle?.Pause();
            SetEnabled(false);
            OnDecision?.Invoke(_requestId, type);

            // Visual feedback
            style.opacity = 0.6f;
        }

        static Button MakeButton(string text, bool isDark, bool primary)
        {
            var btn = new Button { text = text };
            btn.style.fontSize = 11;
            btn.style.height = 24;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;

            if (primary)
            {
                btn.style.backgroundColor = isDark
                    ? new Color(0.2f, 0.35f, 0.55f)
                    : new Color(0.25f, 0.47f, 0.85f);
                btn.style.color = Color.white;
                btn.style.borderTopWidth = 0;
                btn.style.borderBottomWidth = 0;
                btn.style.borderLeftWidth = 0;
                btn.style.borderRightWidth = 0;
            }
            else
            {
                btn.style.backgroundColor = Color.clear;
                var borderColor = isDark
                    ? new Color(0.4f, 0.4f, 0.45f)
                    : new Color(0.6f, 0.6f, 0.65f);
                btn.style.borderTopWidth = 1;
                btn.style.borderBottomWidth = 1;
                btn.style.borderLeftWidth = 1;
                btn.style.borderRightWidth = 1;
                btn.style.borderTopColor = borderColor;
                btn.style.borderBottomColor = borderColor;
                btn.style.borderLeftColor = borderColor;
                btn.style.borderRightColor = borderColor;
                btn.style.color = isDark
                    ? new Color(0.75f, 0.75f, 0.8f)
                    : new Color(0.3f, 0.3f, 0.35f);
            }

            return btn;
        }

        // ── Tool Context Formatting (public static for testability) ──

        /// <summary>
        /// Formats a human-readable context string for a tool call, summarising its key parameters.
        /// </summary>
        /// <param name="tool">The name of the tool being called.</param>
        /// <param name="input">The JSON input object for the tool call, or null.</param>
        /// <returns>A short human-readable description of the tool call context, or an empty string if input is null.</returns>
        public static string FormatToolContext(string tool, JObject input)
        {
            if (input == null) return "";

            switch (tool)
            {
                case "Edit":
                case "Read":
                    return $"File: {input["file_path"]}";

                case "Write":
                    var content = input["content"]?.ToString() ?? "";
                    return $"File: {input["file_path"]} ({content.Length} chars)";

                case "Bash":
                    var cmd = input["command"]?.ToString() ?? "";
                    if (cmd.Length > 200)
                        cmd = cmd.Substring(0, 200) + "...";
                    return $"Command: {cmd}";

                case "Grep":
                    return $"Search: {input["pattern"]} in {input["path"]}";

                case "Glob":
                    return $"Pattern: {input["pattern"]}";

                default:
                    // Generic: show first 2-3 key-value pairs
                    var pairs = input.Properties()
                        .Take(3)
                        .Select(p => $"{p.Name}: {Truncate(p.Value.ToString(), 50)}");
                    return string.Join(", ", pairs);
            }
        }

        static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
