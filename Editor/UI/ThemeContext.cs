using UnityEditor;
using UnityEngine;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// Provides shared theme colors and font sizing to all UniClaude editor UI components.
    /// Pass a single instance by reference to each component that needs theming.
    /// </summary>
    public class ThemeContext
    {
        // ── Internal color pairs ─────────────────────────────────────────────

        static readonly Color _userBubbleDark        = new(0.15f, 0.25f, 0.40f);
        static readonly Color _userBubbleLight       = new(0.78f, 0.86f, 0.96f);
        static readonly Color _assistantBubbleDark   = new(0.24f, 0.24f, 0.27f);
        static readonly Color _assistantBubbleLight  = new(0.91f, 0.91f, 0.91f);
        static readonly Color _systemBubbleDark      = new(0.20f, 0.20f, 0.20f);
        static readonly Color _systemBubbleLight     = new(0.95f, 0.95f, 0.95f);
        static readonly Color _codeBlockDark         = new(0.12f, 0.12f, 0.14f);
        static readonly Color _codeBlockLight        = new(0.88f, 0.88f, 0.88f);
        static readonly Color _inputBorderDark       = new(0.35f, 0.35f, 0.38f);
        static readonly Color _inputBorderLight      = new(0.70f, 0.70f, 0.70f);
        static readonly Color _autocompleteBgDark    = new(0.18f, 0.18f, 0.20f);
        static readonly Color _autocompleteBgLight   = new(0.96f, 0.96f, 0.96f);
        static readonly Color _autocompleteSelDark   = new(0.25f, 0.35f, 0.50f);
        static readonly Color _autocompleteSelLight  = new(0.80f, 0.88f, 1.00f);
        static readonly Color _tabActiveDark         = new(0.35f, 0.55f, 0.85f);
        static readonly Color _tabActiveLight        = new(0.20f, 0.40f, 0.70f);
        static readonly Color _textColorDark         = new(0.85f, 0.85f, 0.85f);
        static readonly Color _dimTextDark           = new(0.50f, 0.50f, 0.55f);
        static readonly Color _dimTextLight          = new(0.45f, 0.45f, 0.50f);
        static readonly Color _systemBorderDark      = new(0.30f, 0.30f, 0.30f);
        static readonly Color _systemBorderLight     = new(0.80f, 0.80f, 0.80f);

        // ── State ────────────────────────────────────────────────────────────

        string _fontPreset = "medium";

        // ── Skin detection ───────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the Unity Editor is using the Pro (dark) skin.
        /// </summary>
        public bool IsDark => EditorGUIUtility.isProSkin;

        // ── Font preset ──────────────────────────────────────────────────────

        /// <summary>
        /// Named font-size preset. Accepted values: "small", "medium", "large", "xlarge".
        /// Null is normalized to "medium".
        /// </summary>
        public string FontPreset
        {
            get => _fontPreset;
            set => _fontPreset = value ?? "medium";
        }

        // ── Color properties ─────────────────────────────────────────────────

        /// <summary>Background color for user chat bubbles.</summary>
        public Color UserBubble => IsDark ? _userBubbleDark : _userBubbleLight;

        /// <summary>Background color for assistant chat bubbles.</summary>
        public Color AssistantBubble => IsDark ? _assistantBubbleDark : _assistantBubbleLight;

        /// <summary>Background color for system/tool chat bubbles.</summary>
        public Color SystemBubble => IsDark ? _systemBubbleDark : _systemBubbleLight;

        /// <summary>Background color for inline code blocks.</summary>
        public Color CodeBlock => IsDark ? _codeBlockDark : _codeBlockLight;

        /// <summary>Border color for the message input field.</summary>
        public Color InputBorder => IsDark ? _inputBorderDark : _inputBorderLight;

        /// <summary>Background color for the autocomplete dropdown.</summary>
        public Color AutocompleteBg => IsDark ? _autocompleteBgDark : _autocompleteBgLight;

        /// <summary>Highlight color for the selected autocomplete entry.</summary>
        public Color AutocompleteSel => IsDark ? _autocompleteSelDark : _autocompleteSelLight;

        /// <summary>Accent color for the active tab indicator.</summary>
        public Color TabActive => IsDark ? _tabActiveDark : _tabActiveLight;

        /// <summary>Primary text color.</summary>
        public Color TextColor => IsDark ? _textColorDark : Color.black;

        /// <summary>Dimmed/secondary text color for metadata and hints.</summary>
        public Color DimText => IsDark ? _dimTextDark : _dimTextLight;

        /// <summary>Border color for system/tool message containers.</summary>
        public Color SystemBorder => IsDark ? _systemBorderDark : _systemBorderLight;

        // ── Font sizing ──────────────────────────────────────────────────────

        /// <summary>
        /// Text size tiers used by <see cref="FontSize"/> to scale UI text proportionally.
        /// </summary>
        public enum FontTier { Title, Header, Body, Tab, Code, Meta, Hint }

        /// <summary>
        /// Returns the pixel font size for the given <paramref name="tier"/>, scaled by the
        /// current <see cref="FontPreset"/>.
        /// </summary>
        /// <param name="tier">The text role whose size is needed.</param>
        /// <returns>Pixel font size as an integer.</returns>
        public int FontSize(FontTier tier)
        {
            int baseSize = FontPreset switch
            {
                "small"  => 11,
                "large"  => 15,
                "xlarge" => 18,
                _        => 13, // medium (default)
            };

            return tier switch
            {
                FontTier.Title  => baseSize + 5,
                FontTier.Header => baseSize + 1,
                FontTier.Tab    => baseSize - 1,
                FontTier.Code   => baseSize - 1,
                FontTier.Meta   => baseSize - 3,
                FontTier.Hint   => baseSize - 4,
                _               => baseSize, // Body
            };
        }
    }
}
