// Editor/UI/Input/ChatInputField.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;
using static UniClaude.Editor.UI.ThemeContext;

namespace UniClaude.Editor.UI.Input
{
    /// <summary>
    /// A multiline text field that auto-grows from 1 to 8 lines, supports internal
    /// scrolling at max height, and provides a drag handle for manual resizing.
    /// </summary>
    public class ChatInputField : VisualElement
    {
        const float MinHeight = 36f;
        const float PaddingAllowance = 12f;
        const int MaxLines = 8;

        readonly TextField _field;
        readonly VisualElement _resizeHandle;
        readonly Label _gripDots;
        readonly ThemeContext _theme;

        float _lineHeight;
        float _maxHeight;
        bool _manualResizeActive;
        bool _isDragging;
        float _dragStartY;
        float _dragStartHeight;

        /// <summary>Gets or sets the text content.</summary>
        public string Text
        {
            get => _field.value;
            set => _field.value = value;
        }

        /// <summary>The underlying TextField for event registration.</summary>
        public TextField Field => _field;

        /// <summary>Fires when the text content changes.</summary>
        public event Action<string> OnTextChanged;

        /// <summary>
        /// Initializes a new <see cref="ChatInputField"/> with the given theme context.
        /// </summary>
        /// <param name="theme">Shared theme context providing colors and font sizes.</param>
        public ChatInputField(ThemeContext theme)
        {
            _theme = theme;
            pickingMode = PickingMode.Ignore; // let clicks pass through to the inner TextField
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.minWidth = 0;
            style.position = Position.Relative;

            // Resize handle — drag target sitting above the text field
            _resizeHandle = new VisualElement();
            _resizeHandle.style.position = Position.Absolute;
            _resizeHandle.style.top = -13;
            _resizeHandle.style.left = 0;
            _resizeHandle.style.right = 0;
            _resizeHandle.style.height = 10;
            _resizeHandle.pickingMode = PickingMode.Position;
            _resizeHandle.RegisterCallback<MouseDownEvent>(OnResizeMouseDown);
            _resizeHandle.RegisterCallback<MouseMoveEvent>(OnResizeMouseMove);
            _resizeHandle.RegisterCallback<MouseUpEvent>(OnResizeMouseUp);

            // Grip dots — persistent visual affordance centered on the handle
            _gripDots = new Label("\u2022 \u2022 \u2022"); // bullet dots
            _gripDots.pickingMode = PickingMode.Ignore;
            _gripDots.style.position = Position.Absolute;
            _gripDots.style.left = 0;
            _gripDots.style.right = 0;
            _gripDots.style.top = 0;
            _gripDots.style.bottom = 0;
            _gripDots.style.unityTextAlign = TextAnchor.MiddleCenter;
            _gripDots.style.fontSize = 8;
            _gripDots.style.color = theme.DimText;
            _gripDots.style.letterSpacing = 3;
            _resizeHandle.Add(_gripDots);

            // Highlight on hover
            _resizeHandle.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _resizeHandle.style.backgroundColor = theme.IsDark
                    ? new Color(0.35f, 0.45f, 0.65f, 0.4f)
                    : new Color(0.3f, 0.4f, 0.6f, 0.3f);
                _resizeHandle.style.borderBottomWidth = 2;
                _resizeHandle.style.borderBottomColor = theme.IsDark
                    ? new Color(0.45f, 0.55f, 0.75f, 0.7f)
                    : new Color(0.35f, 0.45f, 0.65f, 0.6f);
                _gripDots.style.color = theme.IsDark
                    ? new Color(0.75f, 0.8f, 0.9f)
                    : new Color(0.25f, 0.3f, 0.4f);
            });
            _resizeHandle.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!_isDragging)
                {
                    _resizeHandle.style.backgroundColor = Color.clear;
                    _resizeHandle.style.borderBottomWidth = 0;
                    _gripDots.style.color = theme.DimText;
                }
            });

            Add(_resizeHandle);

            // Text field
            _field = new TextField();
            _field.multiline = true;
            _field.style.flexGrow = 1;
            _field.style.minWidth = 0;
            _field.style.whiteSpace = WhiteSpace.Normal;
            _field.style.fontSize = theme.FontSize(FontTier.Body);

            _lineHeight = theme.FontSize(FontTier.Body) + 4f;
            _maxHeight = _lineHeight * MaxLines + PaddingAllowance;

            _field.style.minHeight = MinHeight;
            _field.style.maxHeight = _maxHeight;

            _field.RegisterCallback<ChangeEvent<string>>(OnFieldChanged);
            Add(_field);
        }

        /// <summary>Moves focus to the text field.</summary>
        public new void Focus() => _field.Focus();

        /// <summary>Clears the text and resets to auto-grow mode.</summary>
        public void Clear()
        {
            _field.value = "";
            _manualResizeActive = false;
            UpdateHeight();
        }

        /// <summary>Updates the font size from theme and recalculates limits.</summary>
        public void ApplyFontSizes()
        {
            _field.style.fontSize = _theme.FontSize(FontTier.Body);
            _lineHeight = _theme.FontSize(FontTier.Body) + 4f;
            _maxHeight = _lineHeight * MaxLines + PaddingAllowance;
            _field.style.maxHeight = _maxHeight;
            UpdateHeight();
        }

        // ── Height Calculation (public for testing) ──

        /// <summary>
        /// Computes the desired height based on number of newlines in text.
        /// </summary>
        /// <param name="newlineCount">Number of newline characters in the current text.</param>
        /// <param name="lineHeight">Height per line in pixels.</param>
        /// <param name="minHeight">Minimum allowed height in pixels.</param>
        /// <param name="maxHeight">Maximum allowed height in pixels.</param>
        /// <returns>The clamped desired height in pixels.</returns>
        public static float ComputeDesiredHeight(int newlineCount, float lineHeight,
            float minHeight, float maxHeight)
        {
            var lines = newlineCount + 1;
            var desired = lines * lineHeight + PaddingAllowance;
            return Mathf.Clamp(desired, minHeight, maxHeight);
        }

        /// <summary>Clamps a manual resize height to min/max bounds.</summary>
        /// <param name="height">The requested height in pixels.</param>
        /// <param name="minHeight">Minimum allowed height in pixels.</param>
        /// <param name="maxHeight">Maximum allowed height in pixels.</param>
        /// <returns>The clamped height in pixels.</returns>
        public static float ClampResizeHeight(float height, float minHeight, float maxHeight)
        {
            return Mathf.Clamp(height, minHeight, maxHeight);
        }

        // ── Private ──

        void OnFieldChanged(ChangeEvent<string> evt)
        {
            if (!_manualResizeActive)
                UpdateHeight();
            OnTextChanged?.Invoke(evt.newValue);
        }

        void UpdateHeight()
        {
            if (_manualResizeActive) return;
            var text = _field.value ?? "";
            var newlines = 0;
            foreach (var c in text)
                if (c == '\n') newlines++;

            var desired = ComputeDesiredHeight(newlines, _lineHeight, MinHeight, _maxHeight);
            _field.style.height = desired;
        }

        void OnResizeMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return;
            _isDragging = true;
            _dragStartY = evt.mousePosition.y;
            _dragStartHeight = _field.resolvedStyle.height;
            _resizeHandle.CaptureMouse();
            evt.StopPropagation();
        }

        void OnResizeMouseMove(MouseMoveEvent evt)
        {
            if (!_isDragging) return;
            var delta = _dragStartY - evt.mousePosition.y;
            var newHeight = ClampResizeHeight(_dragStartHeight + delta, MinHeight, _maxHeight);
            _field.style.height = newHeight;
            _manualResizeActive = true;
        }

        void OnResizeMouseUp(MouseUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            if (_resizeHandle.HasMouseCapture())
                _resizeHandle.ReleaseMouse();
            _resizeHandle.style.backgroundColor = Color.clear;
            _resizeHandle.style.borderBottomWidth = 0;
            _gripDots.style.color = _theme.DimText;
        }
    }
}
