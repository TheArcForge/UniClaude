using System;
using System.Text.RegularExpressions;
using UniClaude.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// Static helpers for creating and styling chat message bubbles, rendering markdown
    /// with code blocks, creating selectable text fields, file path links, and
    /// formatting elapsed time. All methods accept a <see cref="ThemeContext"/> instead
    /// of accessing instance fields, making them reusable across multiple panels.
    /// </summary>
    public static class MessageRenderer
    {
        // ── Compiled regex fields ────────────────────────────────────────────

        /// <summary>Matches <c>**bold**</c> markdown syntax.</summary>
        static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        /// <summary>Matches <c>*italic*</c> markdown syntax without conflicting with bold markers.</summary>
        static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);

        /// <summary>Matches inline <c>`code`</c> spans (single backtick, not asset paths).</summary>
        static readonly Regex InlineCodeRegex = new(@"`([^`]+?)`", RegexOptions.Compiled);

        /// <summary>Matches backtick-wrapped Unity asset paths (Assets/… or Packages/…).</summary>
        static readonly Regex AssetPathRegex = new(@"`((?:Assets|Packages)/[^`]+?\.\w+)`", RegexOptions.Compiled);

        // ── Bubble creation ──────────────────────────────────────────────────

        /// <summary>
        /// Creates a new <see cref="VisualElement"/> and applies bubble styling via
        /// <see cref="StyleBubble"/>.
        /// </summary>
        /// <param name="theme">Theme context supplying colors and font sizes.</param>
        /// <param name="role">Message role that determines bubble color and alignment.</param>
        /// <returns>A styled <see cref="VisualElement"/> ready to receive content.</returns>
        public static VisualElement CreateBubble(ThemeContext theme, MessageRole role)
        {
            var bubble = new VisualElement();
            StyleBubble(bubble, theme, role);
            return bubble;
        }

        /// <summary>
        /// Applies background color, margins, radius, padding, and (for System role) a
        /// 1 px border to <paramref name="bubble"/> based on the given <paramref name="role"/>.
        /// </summary>
        /// <param name="bubble">The element to style.</param>
        /// <param name="theme">Theme context supplying colors.</param>
        /// <param name="role">Determines which color set and margin values to apply.</param>
        public static void StyleBubble(VisualElement bubble, ThemeContext theme, MessageRole role)
        {
            Color bgColor;
            float marginLeft, marginRight;

            switch (role)
            {
                case MessageRole.User:
                    bgColor = theme.UserBubble;
                    marginLeft = 60;
                    marginRight = 4;
                    break;
                case MessageRole.System:
                    bgColor = theme.SystemBubble;
                    marginLeft = 20;
                    marginRight = 20;
                    break;
                default:
                    bgColor = theme.AssistantBubble;
                    marginLeft = 4;
                    marginRight = 60;
                    break;
            }

            bubble.style.backgroundColor = bgColor;
            bubble.style.borderTopLeftRadius = 8;
            bubble.style.borderTopRightRadius = 8;
            bubble.style.borderBottomLeftRadius = 8;
            bubble.style.borderBottomRightRadius = 8;
            bubble.style.paddingTop = 8;
            bubble.style.paddingBottom = 8;
            bubble.style.paddingLeft = 12;
            bubble.style.paddingRight = 12;
            bubble.style.marginTop = 4;
            bubble.style.marginBottom = 4;
            bubble.style.marginLeft = marginLeft;
            bubble.style.marginRight = marginRight;

            if (role == MessageRole.System)
            {
                bubble.style.borderTopWidth = 1;
                bubble.style.borderBottomWidth = 1;
                bubble.style.borderLeftWidth = 1;
                bubble.style.borderRightWidth = 1;
                var borderColor = theme.SystemBorder;
                bubble.style.borderTopColor = borderColor;
                bubble.style.borderBottomColor = borderColor;
                bubble.style.borderLeftColor = borderColor;
                bubble.style.borderRightColor = borderColor;
            }
        }

        // ── Role label ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a bold role label in Meta font size with a dimmed color and a small
        /// bottom margin. Uses inline color values (dark: 0.6,0.6,0.65 / light: 0.4,0.4,0.45)
        /// rather than <see cref="ThemeContext.DimText"/> to match the original source exactly.
        /// </summary>
        /// <param name="theme">Theme context supplying skin detection and font size.</param>
        /// <param name="role">Display string shown in the label (e.g. "You", "Claude").</param>
        /// <returns>A styled <see cref="Label"/>.</returns>
        public static Label MakeRoleLabel(ThemeContext theme, string role)
        {
            var label = new Label(role);
            label.style.fontSize = theme.FontSize(ThemeContext.FontTier.Meta);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = theme.IsDark
                ? new Color(0.6f, 0.6f, 0.65f)
                : new Color(0.4f, 0.4f, 0.45f);
            label.style.marginBottom = 4;
            return label;
        }

        // ── Code-block rendering ─────────────────────────────────────────────

        /// <summary>
        /// Splits <paramref name="content"/> on triple-backtick fences and adds each
        /// segment to <paramref name="parent"/>. Even-indexed segments are plain text
        /// (rendered via <see cref="AddTextWithFileLinks"/>); odd-indexed segments are
        /// fenced code blocks with a green-tinted font and a Copy button.
        /// The language identifier on the first line of a code block (within the first
        /// 20 characters) is automatically stripped.
        /// </summary>
        /// <param name="parent">Container element that receives the generated children.</param>
        /// <param name="content">Raw message content potentially containing code fences.</param>
        /// <param name="theme">Theme context supplying colors and font sizes.</param>
        public static void AddContentWithCodeBlocks(VisualElement parent, string content, ThemeContext theme)
        {
            var parts = content.Split(new[] { "```" }, StringSplitOptions.None);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 0)
                {
                    var text = parts[i].Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var textColor = theme.IsDark ? new Color(0.85f, 0.85f, 0.85f) : Color.black;
                        AddTextWithFileLinks(parent, text, textColor, theme.FontSize(ThemeContext.FontTier.Body), theme);
                    }
                }
                else
                {
                    var code = parts[i];
                    int newlineIndex = code.IndexOf('\n');
                    if (newlineIndex >= 0 && newlineIndex < 20)
                        code = code.Substring(newlineIndex + 1);
                    code = code.Trim();

                    if (!string.IsNullOrEmpty(code))
                    {
                        var codeBlock = new VisualElement();
                        codeBlock.style.backgroundColor = theme.CodeBlock;
                        codeBlock.style.borderTopLeftRadius = 4;
                        codeBlock.style.borderTopRightRadius = 4;
                        codeBlock.style.borderBottomLeftRadius = 4;
                        codeBlock.style.borderBottomRightRadius = 4;
                        codeBlock.style.paddingTop = 8;
                        codeBlock.style.paddingBottom = 8;
                        codeBlock.style.paddingLeft = 10;
                        codeBlock.style.paddingRight = 10;
                        codeBlock.style.marginTop = 6;
                        codeBlock.style.marginBottom = 6;

                        var codeColor = theme.IsDark
                            ? new Color(0.78f, 0.85f, 0.78f)
                            : new Color(0.15f, 0.35f, 0.15f);
                        var codeField = MakeSelectableText(theme, code, codeColor, preWrap: true, fontSize: theme.FontSize(ThemeContext.FontTier.Code));
                        codeBlock.Add(codeField);

                        var codeText = code;
                        var copyBtn = new Button(() => EditorGUIUtility.systemCopyBuffer = codeText)
                            { text = "Copy" };
                        copyBtn.style.alignSelf = Align.FlexEnd;
                        copyBtn.style.fontSize = theme.FontSize(ThemeContext.FontTier.Hint);
                        copyBtn.style.height = 18;
                        copyBtn.style.marginTop = 4;
                        codeBlock.Add(copyBtn);

                        parent.Add(codeBlock);
                    }
                }
            }
        }

        // ── Selectable text ──────────────────────────────────────────────────

        /// <summary>
        /// Creates a read-only multiline <see cref="TextField"/> styled to look like a plain
        /// label, stripping all chrome (background, borders, margins, padding) from both the
        /// field and its inner <c>.unity-text-field__input</c> element.
        /// When <paramref name="richText"/> is <c>true</c>, <paramref name="text"/> is first
        /// processed by <see cref="MarkdownToRichText"/> and rich text is enabled on the inner
        /// <see cref="TextElement"/>.
        /// </summary>
        /// <param name="theme">Theme context used for Markdown color conversion.</param>
        /// <param name="text">Raw text content to display.</param>
        /// <param name="color">Foreground text color.</param>
        /// <param name="preWrap">
        /// When <c>true</c>, applies <see cref="WhiteSpace.PreWrap"/> to preserve whitespace
        /// and line breaks (suitable for code). Defaults to <c>false</c>.
        /// </param>
        /// <param name="fontSize">
        /// Pixel font size. Pass <c>0</c> (default) to leave the font size unset so the
        /// parent's inherited size applies.
        /// </param>
        /// <param name="richText">
        /// When <c>true</c>, converts markdown to Unity rich text before display.
        /// Defaults to <c>false</c>.
        /// </param>
        /// <returns>A styled, read-only <see cref="TextField"/>.</returns>
        public static TextField MakeSelectableText(ThemeContext theme, string text, Color color, bool preWrap = false, int fontSize = 0, bool richText = false)
        {
            var displayText = richText ? MarkdownToRichText(text, theme.IsDark) : text;
            var field = new TextField { value = displayText, isReadOnly = true, multiline = true };
            field.style.whiteSpace = preWrap ? WhiteSpace.PreWrap : WhiteSpace.Normal;
            field.style.color = color;
            if (fontSize > 0)
                field.style.fontSize = fontSize;

            // Strip TextField chrome so it looks like a plain label
            field.style.backgroundColor = Color.clear;
            field.style.borderTopWidth = 0;
            field.style.borderBottomWidth = 0;
            field.style.borderLeftWidth = 0;
            field.style.borderRightWidth = 0;
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
            field.style.marginLeft = 0;
            field.style.marginRight = 0;
            field.style.paddingTop = 0;
            field.style.paddingBottom = 0;
            field.style.paddingLeft = 0;
            field.style.paddingRight = 0;

            // Also strip the inner TextInput element's chrome
            var input = field.Q(className: "unity-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = Color.clear;
                input.style.borderTopWidth = 0;
                input.style.borderBottomWidth = 0;
                input.style.borderLeftWidth = 0;
                input.style.borderRightWidth = 0;
                input.style.paddingTop = 0;
                input.style.paddingBottom = 0;
                input.style.paddingLeft = 0;
                input.style.paddingRight = 0;
            }

            // Enable rich text on the inner TextElement
            if (richText)
            {
                var textElement = field.Q<TextElement>();
                if (textElement != null)
                    textElement.enableRichText = true;
            }

            return field;
        }

        // ── Markdown conversion ──────────────────────────────────────────────

        /// <summary>
        /// Converts a subset of inline Markdown to Unity rich text tags.
        /// Processes in order: bold (<c>**…**</c>), italic (<c>*…*</c>), then inline
        /// code spans (<c>`…`</c>). Inline code color is theme-dependent.
        /// </summary>
        /// <param name="text">Raw Markdown text to convert.</param>
        /// <param name="isDark">
        /// <c>true</c> when the Editor is using the Pro (dark) skin; controls the inline
        /// code color (#d4c4a0 dark / #6e5430 light).
        /// </param>
        /// <returns>Text with Unity rich text tags inserted.</returns>
        public static string MarkdownToRichText(string text, bool isDark)
        {
            // Bold: **text** → <b>text</b>  (must run before italic)
            text = BoldRegex.Replace(text, "<b>$1</b>");
            // Italic: *text* → <i>text</i>
            text = ItalicRegex.Replace(text, "<i>$1</i>");
            // Inline code: `text` → tinted color (brighter on dark, darker on light)
            var codeColor = isDark ? "#d4c4a0" : "#6e5430";
            text = InlineCodeRegex.Replace(text, $"<color={codeColor}>$1</color>");
            return text;
        }

        // ── File link rendering ──────────────────────────────────────────────

        /// <summary>
        /// Adds text content to <paramref name="parent"/>, with backtick-wrapped Unity asset
        /// paths rendered as clickable labels that ping the asset in the Project window and
        /// set it as the active selection. Non-path text is rendered via
        /// <see cref="MakeSelectableText"/> with rich text enabled. When asset paths are
        /// present, a flex-wrap flow container interleaves rich text labels and link labels.
        /// </summary>
        /// <param name="parent">Container element that receives the generated children.</param>
        /// <param name="text">Raw message text potentially containing backtick-wrapped asset paths.</param>
        /// <param name="textColor">Foreground color for non-link text segments.</param>
        /// <param name="fontSize">Pixel font size applied to all generated labels.</param>
        /// <param name="theme">Theme context supplying skin detection for link color.</param>
        public static void AddTextWithFileLinks(VisualElement parent, string text, Color textColor, int fontSize, ThemeContext theme)
        {
            var matches = AssetPathRegex.Matches(text);
            if (matches.Count == 0)
            {
                parent.Add(MakeSelectableText(theme, text, textColor, fontSize: fontSize, richText: true));
                return;
            }

            // Wrap segments in a flow container
            var flow = new VisualElement();
            flow.style.flexDirection = FlexDirection.Row;
            flow.style.flexWrap = Wrap.Wrap;
            flow.style.alignItems = Align.FlexEnd;

            int lastIndex = 0;
            foreach (Match match in matches)
            {
                // Text before the match
                if (match.Index > lastIndex)
                {
                    var before = MarkdownToRichText(text.Substring(lastIndex, match.Index - lastIndex), theme.IsDark);
                    var label = new Label(before);
                    label.enableRichText = true;
                    label.style.fontSize = fontSize;
                    label.style.color = textColor;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    flow.Add(label);
                }

                // Clickable file path
                var path = match.Groups[1].Value;
                var linkColor = theme.IsDark ? new Color(0.5f, 0.7f, 1f) : new Color(0.1f, 0.3f, 0.8f);
                var link = new Label($"`{path}`");
                link.style.fontSize = fontSize;
                link.style.color = linkColor;
                link.style.whiteSpace = WhiteSpace.NoWrap;
                link.style.borderBottomWidth = 1;
                link.style.borderBottomColor = linkColor;

                var assetPath = path;
                link.RegisterCallback<ClickEvent>(_ =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                    else
                    {
                        Debug.LogWarning($"[UniClaude] Asset not found: {assetPath}");
                    }
                });

                flow.Add(link);
                lastIndex = match.Index + match.Length;
            }

            // Text after the last match
            if (lastIndex < text.Length)
            {
                var after = MarkdownToRichText(text.Substring(lastIndex), theme.IsDark);
                var label = new Label(after);
                label.enableRichText = true;
                label.style.fontSize = fontSize;
                label.style.color = textColor;
                label.style.whiteSpace = WhiteSpace.Normal;
                flow.Add(label);
            }

            parent.Add(flow);
        }

        // ── Formatting helpers ───────────────────────────────────────────────

        /// <summary>
        /// Formats an elapsed duration as a compact human-readable string.
        /// Returns <c>"{n}s"</c> for durations under one minute, or
        /// <c>"{m}m {ss}s"</c> (zero-padded seconds) for longer durations.
        /// </summary>
        /// <param name="seconds">Elapsed time in seconds.</param>
        /// <returns>Formatted elapsed time string.</returns>
        public static string FormatElapsed(double seconds)
        {
            if (seconds < 60)
                return $"{(int)seconds}s";
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins}m {secs:D2}s";
        }
    }
}
