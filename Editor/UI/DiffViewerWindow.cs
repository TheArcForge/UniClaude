using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// Displays a colored diff view for file modifications.
    /// Auto-closes when the window loses focus.
    /// </summary>
    public class DiffViewerWindow : EditorWindow
    {
        string _diffText;
        string _filePath;

        static readonly Color DarkAddedBg = new(0.15f, 0.25f, 0.15f);
        static readonly Color DarkRemovedBg = new(0.3f, 0.12f, 0.12f);
        static readonly Color DarkContextBg = new(0.16f, 0.16f, 0.18f);
        static readonly Color DarkAddedText = new(0.5f, 0.9f, 0.5f);
        static readonly Color DarkRemovedText = new(0.9f, 0.5f, 0.5f);
        static readonly Color DarkContextText = new(0.65f, 0.65f, 0.7f);
        static readonly Color DarkGutterText = new(0.4f, 0.4f, 0.45f);

        static readonly Color LightAddedBg = new(0.85f, 0.95f, 0.85f);
        static readonly Color LightRemovedBg = new(0.95f, 0.85f, 0.85f);
        static readonly Color LightContextBg = new(0.94f, 0.94f, 0.94f);
        static readonly Color LightAddedText = new(0.1f, 0.5f, 0.1f);
        static readonly Color LightRemovedText = new(0.6f, 0.15f, 0.15f);
        static readonly Color LightContextText = new(0.3f, 0.3f, 0.35f);
        static readonly Color LightGutterText = new(0.55f, 0.55f, 0.6f);

        /// <summary>
        /// Opens the diff viewer with the given file path and diff text.
        /// </summary>
        public static void Show(string filePath, string diffText)
        {
            var window = GetWindow<DiffViewerWindow>(false, System.IO.Path.GetFileName(filePath));
            window._filePath = filePath;
            window._diffText = diffText;
            window.minSize = new Vector2(500, 300);
            window.BuildUI();
            window.Show();
        }

        void OnLostFocus()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null) Close();
            };
        }

        static Font LoadMonoFont()
        {
#if UNITY_EDITOR_OSX
            string[] candidates = { "Menlo", "Monaco", "Courier New" };
#elif UNITY_EDITOR_WIN
            string[] candidates = { "Consolas", "Courier New", "Lucida Console" };
#else
            string[] candidates = { "DejaVu Sans Mono", "Liberation Mono", "Courier New" };
#endif
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 12);
                if (f != null) return f;
            }
            return null;
        }

        void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var isDark = EditorGUIUtility.isProSkin;

            var monoFont = LoadMonoFont();

            root.style.backgroundColor = isDark ? DarkContextBg : LightContextBg;

            // Header
            var header = new Label(_filePath);
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.color = isDark ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.2f, 0.2f, 0.25f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = isDark ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.75f, 0.75f, 0.78f);
            root.Add(header);

            // Scrollable diff content
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            root.Add(scroll);

            if (string.IsNullOrEmpty(_diffText))
            {
                var empty = new Label("No diff data available.");
                empty.style.paddingTop = 20;
                empty.style.paddingLeft = 12;
                empty.style.color = isDark ? DarkContextText : LightContextText;
                scroll.Add(empty);
                return;
            }

            var lines = _diffText.Split('\n');
            int lineNum = 0;

            foreach (var rawLine in lines)
            {
                lineNum++;
                var line = rawLine.TrimEnd('\r');

                Color bgColor, textColor;

                if (line.StartsWith("+ ") || line.StartsWith("+\t") || line == "+")
                {
                    bgColor = isDark ? DarkAddedBg : LightAddedBg;
                    textColor = isDark ? DarkAddedText : LightAddedText;
                }
                else if (line.StartsWith("- ") || line.StartsWith("-\t") || line == "-")
                {
                    bgColor = isDark ? DarkRemovedBg : LightRemovedBg;
                    textColor = isDark ? DarkRemovedText : LightRemovedText;
                }
                else
                {
                    bgColor = isDark ? DarkContextBg : LightContextBg;
                    textColor = isDark ? DarkContextText : LightContextText;
                }

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.backgroundColor = bgColor;
                row.style.paddingLeft = 4;
                row.style.paddingRight = 8;
                row.style.minHeight = 18;

                // Line number gutter
                var gutter = new Label(lineNum.ToString());
                gutter.style.width = 40;
                gutter.style.unityTextAlign = TextAnchor.MiddleRight;
                gutter.style.fontSize = 12;
                gutter.style.color = isDark ? DarkGutterText : LightGutterText;
                gutter.style.paddingRight = 8;
                gutter.style.unityFontStyleAndWeight = FontStyle.Normal;
                if (monoFont != null) gutter.style.unityFont = new StyleFont(monoFont);
                row.Add(gutter);

                // Line content
                var content = new Label(line);
                content.style.fontSize = 12;
                content.style.color = textColor;
                content.style.whiteSpace = WhiteSpace.Pre;
                content.style.flexGrow = 1;
                content.enableRichText = false;
                if (monoFont != null) content.style.unityFont = new StyleFont(monoFont);
                row.Add(content);

                scroll.Add(row);
            }
        }
    }
}
