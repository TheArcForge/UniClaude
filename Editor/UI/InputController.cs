using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniClaude.Editor;
using UniClaude.Editor.UI.Input;
using static UniClaude.Editor.UI.ThemeContext;

namespace UniClaude.Editor.UI
{
    /// <summary>
    /// A <see cref="VisualElement"/> that owns the chat input area: text field, send button,
    /// autocomplete popup, arg choice popup, hint strip, attachment chip strip, and suggestion display.
    /// Handles all keyboard shortcuts for autocomplete navigation and message submission.
    /// </summary>
    public class InputController : VisualElement
    {
        // ── Fields ───────────────────────────────────────────────────────────

        readonly ThemeContext _theme;
        readonly SlashCommandRegistry _commands;

        ChatInputField _chatInputField;
        AttachmentManager _attachmentManager;
        AttachmentChipStrip _attachmentChipStrip;
        Button _sendButton;
        VisualElement _hintStrip;
        Label _hintLabel;
        bool _hintExpanded;
        string _cachedModelLabel = "Default";
        string _cachedEffortLabel = "Default";
        bool _cachedPlanMode;
        Label _suggestionHint;
        string _pendingSuggestion;
        VisualElement _autocompletePopup;
        List<SlashCommand> _autocompleteMatches = new();
        int _autocompleteSelection = -1;
        bool _autocompleteVisible;
        SlashCommand _argChoiceCommand;
        List<ArgChoice> _argChoiceMatches = new();
        int _argChoiceSelection = -1;

        bool IsShowingArgChoices => _argChoiceCommand != null;

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the user submits a message via the Enter key or the Send button.
        /// The parameter contains the trimmed input text and any file attachments.
        /// </summary>
        public event Action<MessageSubmission> OnSubmit;

        /// <summary>
        /// Fired when the user presses Escape outside of autocomplete to cancel generation.
        /// </summary>
        public event Action OnCancelRequested;

        // ── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Creates and wires up the full input area: autocomplete popup, hint strip,
        /// attachment chip strip, and input row.
        /// </summary>
        /// <param name="theme">Shared theme context providing colors and font sizes.</param>
        /// <param name="commands">Registry used to resolve slash-command autocomplete matches.</param>
        public InputController(ThemeContext theme, SlashCommandRegistry commands)
        {
            _theme = theme;
            _commands = commands;
            _hintExpanded = SessionState.GetBool("UniClaude_HintExpanded", true);

            var stagingPath = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "..", "Temp", "UniClaude", "Attachments");
            _attachmentManager = new AttachmentManager(stagingPath);

            style.flexShrink = 0;

            BuildAutocompletePopup();
            BuildHintStrip();
            BuildAttachmentStrip();
            BuildInputRow();
            RegisterDragAndDrop();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Gets or sets the current text in the input field.</summary>
        public string Text
        {
            get => _chatInputField.Text;
            set => _chatInputField.Text = value;
        }

        /// <summary>Clears the input field and all attachments.</summary>
        public void Clear()
        {
            _chatInputField.Clear();
            _attachmentManager.ClearAll();
        }

        /// <summary>
        /// Moves keyboard focus to the input field.
        /// Hides the base <see cref="VisualElement.Focus"/> method.
        /// </summary>
        public new void Focus() => _chatInputField?.Focus();

        /// <summary>
        /// Shows a Tab-to-accept suggestion hint below the input field.
        /// Only shown when the input field is empty.
        /// </summary>
        /// <param name="suggestion">The suggestion text to display and accept on Tab.</param>
        public void SetSuggestion(string suggestion)
        {
            if (!string.IsNullOrEmpty(_chatInputField?.Text)) return;
            _pendingSuggestion = suggestion;
            _suggestionHint.text = $"Tab: {suggestion}";
            _suggestionHint.style.display = DisplayStyle.Flex;
        }

        /// <summary>Clears the pending suggestion and hides the suggestion hint label.</summary>
        public void ClearSuggestion()
        {
            _pendingSuggestion = null;
            if (_suggestionHint != null)
                _suggestionHint.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Updates the hint strip label text to reflect current model, effort, and plan mode settings.
        /// </summary>
        /// <param name="modelLabel">Human-readable model name to display.</param>
        /// <param name="effortLabel">Human-readable effort level to display.</param>
        /// <param name="planMode">When <c>true</c>, appends a Plan Mode indicator.</param>
        public void UpdateHintText(string modelLabel, string effortLabel, bool planMode)
        {
            if (_hintLabel == null) return;
            _cachedModelLabel = modelLabel;
            _cachedEffortLabel = effortLabel;
            _cachedPlanMode = planMode;
            RenderHintLabel();
        }

        /// <summary>
        /// Refreshes the input field font size from the current theme body size.
        /// Call after the font preset changes.
        /// </summary>
        public void ApplyFontSizes()
        {
            _chatInputField?.ApplyFontSizes();
        }

        // ── Build Methods ────────────────────────────────────────────────────

        /// <summary>
        /// Builds the autocomplete popup container and adds it to this element.
        /// The popup is initially hidden and positioned above the input row.
        /// </summary>
        void BuildAutocompletePopup()
        {
            _autocompletePopup = new VisualElement();
            _autocompletePopup.style.display = DisplayStyle.None;
            _autocompletePopup.style.backgroundColor = _theme.AutocompleteBg;
            _autocompletePopup.style.borderTopLeftRadius = 6;
            _autocompletePopup.style.borderTopRightRadius = 6;
            _autocompletePopup.style.borderTopWidth = 1;
            _autocompletePopup.style.borderLeftWidth = 1;
            _autocompletePopup.style.borderRightWidth = 1;
            _autocompletePopup.style.borderBottomWidth = 1;
            _autocompletePopup.style.borderTopColor = _theme.InputBorder;
            _autocompletePopup.style.borderLeftColor = _theme.InputBorder;
            _autocompletePopup.style.borderRightColor = _theme.InputBorder;
            _autocompletePopup.style.borderBottomColor = _theme.InputBorder;
            _autocompletePopup.style.marginLeft = 8;
            _autocompletePopup.style.marginRight = 63; // align with input field (send button width + margins)
            _autocompletePopup.style.maxHeight = 200;
            _autocompletePopup.style.overflow = Overflow.Hidden;
            _autocompletePopup.style.paddingTop = 4;
            _autocompletePopup.style.paddingBottom = 4;

            Add(_autocompletePopup);
        }

        /// <summary>
        /// Builds the collapsible hint strip that shows current model, effort, and plan mode.
        /// </summary>
        void BuildHintStrip()
        {
            _hintStrip = new VisualElement();
            _hintStrip.style.flexDirection = FlexDirection.Row;
            _hintStrip.style.alignItems = Align.Center;
            _hintStrip.style.paddingLeft = 10;
            _hintStrip.style.paddingRight = 10;
            _hintStrip.style.paddingTop = 4;
            _hintStrip.style.paddingBottom = 2;

            _hintLabel = new Label();
            _hintLabel.style.fontSize = _theme.FontSize(FontTier.Hint);
            _hintLabel.style.color = _theme.DimText;
            _hintLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _hintLabel.AddToClassList("unity-cursor-link");
            _hintLabel.RegisterCallback<ClickEvent>(_ => ToggleHintExpanded());
            _hintStrip.Add(_hintLabel);

            Add(_hintStrip);
        }

        /// <summary>
        /// Builds the attachment chip strip and adds it to this element.
        /// </summary>
        void BuildAttachmentStrip()
        {
            _attachmentChipStrip = new AttachmentChipStrip(_attachmentManager, _theme);
            Add(_attachmentChipStrip);
        }

        /// <summary>
        /// Builds the suggestion hint label, the input row, chat input field, and send button.
        /// </summary>
        void BuildInputRow()
        {
            _suggestionHint = new Label();
            _suggestionHint.style.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            _suggestionHint.style.fontSize = _theme.FontSize(FontTier.Hint);
            _suggestionHint.style.marginLeft = 4;
            _suggestionHint.style.marginBottom = 2;
            _suggestionHint.style.display = DisplayStyle.None;
            Add(_suggestionHint);

            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.paddingTop = 16;
            inputRow.style.paddingBottom = 6;
            inputRow.style.paddingLeft = 8;
            inputRow.style.paddingRight = 8;
            inputRow.style.borderTopWidth = 1;
            inputRow.style.borderTopColor = _theme.InputBorder;

            _chatInputField = new ChatInputField(_theme);
            _chatInputField.Field.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            _chatInputField.Field.RegisterCallback<KeyDownEvent>(OnPasteCheck, TrickleDown.TrickleDown);
            _chatInputField.OnTextChanged += OnInputTextChanged;

            inputRow.Add(_chatInputField);

            _sendButton = new Button(RaiseTrySend) { text = "Send" };
            _sendButton.style.width = 55;
            _sendButton.style.height = 36;
            _sendButton.style.marginLeft = 4;
            inputRow.Add(_sendButton);

            Add(inputRow);
        }

        // ── Submit ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="MessageSubmission"/> from the current text and attachments,
        /// then fires <see cref="OnSubmit"/> if it has content.
        /// </summary>
        void RaiseTrySend()
        {
            var text = _chatInputField.Text?.Trim() ?? "";
            var submission = new MessageSubmission(text, new List<AttachmentInfo>(_attachmentManager.Attachments));
            if (submission.HasContent)
                OnSubmit?.Invoke(submission);
        }

        // ── Hint Strip ───────────────────────────────────────────────────────

        /// <summary>
        /// Toggles the hint strip between expanded and collapsed state, persisting the
        /// preference to <see cref="SessionState"/>.
        /// </summary>
        void ToggleHintExpanded()
        {
            _hintExpanded = !_hintExpanded;
            SessionState.SetBool("UniClaude_HintExpanded", _hintExpanded);
            RenderHintLabel();
        }

        void RenderHintLabel()
        {
            if (_hintLabel == null) return;
            if (_hintExpanded)
            {
                var planSuffix = _cachedPlanMode ? "  \u00b7  Plan Mode" : "";
                _hintLabel.text = $"\u25be  Model: {_cachedModelLabel}  \u00b7  Effort: {_cachedEffortLabel}{planSuffix}";
            }
            else
            {
                _hintLabel.text = "\u25b8 ...";
            }
        }

        // ── Keyboard Handler ─────────────────────────────────────────────────

        /// <summary>
        /// Handles key-down events on the text field for autocomplete navigation,
        /// suggestion acceptance, and message submission.
        /// </summary>
        void OnInputKeyDown(KeyDownEvent evt)
        {
            if (_autocompleteVisible)
            {
                if (IsShowingArgChoices)
                {
                    // Arg choice navigation
                    switch (evt.keyCode)
                    {
                        case KeyCode.Tab:
                        case KeyCode.Return when !evt.shiftKey:
                            evt.StopImmediatePropagation();
                            evt.PreventDefault();
                            if (_argChoiceSelection >= 0)
                                AcceptArgChoice(_argChoiceSelection);
                            return;

                        case KeyCode.UpArrow:
                            evt.StopImmediatePropagation();
                            if (_argChoiceSelection > 0)
                            {
                                _argChoiceSelection--;
                                RebuildArgChoiceItems();
                            }
                            return;

                        case KeyCode.DownArrow:
                            evt.StopImmediatePropagation();
                            if (_argChoiceSelection < _argChoiceMatches.Count - 1)
                            {
                                _argChoiceSelection++;
                                RebuildArgChoiceItems();
                            }
                            return;

                        case KeyCode.Escape:
                            evt.StopImmediatePropagation();
                            HideAutocomplete();
                            return;
                    }
                }
                else
                {
                    // Command autocomplete navigation
                    switch (evt.keyCode)
                    {
                        case KeyCode.Tab:
                        case KeyCode.Return when !evt.shiftKey:
                            evt.StopImmediatePropagation();
                            evt.PreventDefault();
                            if (_autocompleteSelection >= 0)
                                AcceptAutocomplete(_autocompleteSelection);
                            return;

                        case KeyCode.UpArrow:
                            evt.StopImmediatePropagation();
                            if (_autocompleteSelection > 0)
                            {
                                _autocompleteSelection--;
                                RebuildAutocompleteItems();
                            }
                            return;

                        case KeyCode.DownArrow:
                            evt.StopImmediatePropagation();
                            if (_autocompleteSelection < _autocompleteMatches.Count - 1)
                            {
                                _autocompleteSelection++;
                                RebuildAutocompleteItems();
                            }
                            return;

                        case KeyCode.Escape:
                            evt.StopImmediatePropagation();
                            HideAutocomplete();
                            return;
                    }
                }
            }

            // Tab to accept a prompt suggestion (takes priority over autocomplete trigger)
            if (evt.keyCode == KeyCode.Tab && _pendingSuggestion != null)
            {
                evt.StopImmediatePropagation();
                _chatInputField.Text = _pendingSuggestion;
                _pendingSuggestion = null;
                _suggestionHint.style.display = DisplayStyle.None;
                return;
            }

            // Tab with / prefix but no popup yet — trigger autocomplete
            if (evt.keyCode == KeyCode.Tab)
            {
                var text = _chatInputField.Text?.Trim();
                if (!string.IsNullOrEmpty(text) && text[0] == '/')
                {
                    evt.StopImmediatePropagation();
                    UpdateAutocomplete(text);
                    return;
                }
            }

            // Enter / Shift+Enter handling
            // Unity fires KeyDownEvent twice for Return: once with keyCode, once with character.
            // We must swallow both to prevent the TextField's internal handler from interfering.
            if (evt.keyCode == KeyCode.Return || evt.character == '\n')
            {
                evt.StopImmediatePropagation();
                evt.PreventDefault();

                // Only act on the keyCode event, ignore the duplicate character event
                if (evt.keyCode == KeyCode.Return)
                {
                    if (evt.shiftKey)
                        InsertNewlineAtCursor();
                    else
                    {
                        HideAutocomplete();
                        RaiseTrySend();
                    }
                }
                return;
            }

            // Escape to cancel generation (when autocomplete is not visible)
            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopImmediatePropagation();
                OnCancelRequested?.Invoke();
            }
        }

        /// <summary>
        /// Intercepts Cmd/Ctrl+V to detect clipboard file paths and add them as attachments
        /// instead of pasting the path text into the field.
        /// </summary>
        void OnPasteCheck(KeyDownEvent evt)
        {
            bool isPaste = (evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.V;
            if (!isPaste) return;

            // Check for file path in clipboard
            var clipText = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clipText) && System.IO.File.Exists(clipText.Trim()))
            {
                evt.StopImmediatePropagation();
                evt.PreventDefault();
                _attachmentManager.AddFile(clipText.Trim());
            }
        }

        /// <summary>
        /// Reacts to text changes in the input field: clears suggestions and updates
        /// the autocomplete popup state based on the new text.
        /// </summary>
        void OnInputTextChanged(string text)
        {
            if (_pendingSuggestion != null)
            {
                _pendingSuggestion = null;
                _suggestionHint.style.display = DisplayStyle.None;
            }

            if (string.IsNullOrEmpty(text) || text[0] != '/')
            {
                HideAutocomplete();
                return;
            }

            // Check if we're in "command args" mode: /command <partial-arg>
            var spaceIdx = text.IndexOf(' ');
            if (spaceIdx > 0)
            {
                var cmdName = text.Substring(1, spaceIdx - 1);
                var cmd = _commands.Find(cmdName);
                if (cmd?.ArgChoices != null && cmd.ArgChoices.Count > 0)
                {
                    var argFilter = text.Substring(spaceIdx + 1);
                    ShowArgChoices(cmd, argFilter);
                    return;
                }
                HideAutocomplete();
                return;
            }

            // Still typing command name
            UpdateAutocomplete(text);
        }

        // ── Newline Insertion ─────────────────────────────────────────────────

        void InsertNewlineAtCursor()
        {
            var field = _chatInputField.Field;
            var text = field.value ?? "";
            var cursor = field.cursorIndex;

            field.value = text.Insert(cursor, "\n");

            // Reposition cursor after the newline within UIToolkit's own update cycle
            var newPos = cursor + 1;
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectRange(newPos, newPos);
            });
        }

        // ── Drag and Drop ────────────────────────────────────────────────────

        /// <summary>
        /// Registers drag-and-drop event handlers so files dragged onto the input area
        /// are added as attachments rather than dropped into the text field.
        /// </summary>
        void RegisterDragAndDrop()
        {
            RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });

            RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.paths == null) return;
                foreach (var path in DragAndDrop.paths)
                    _attachmentManager.AddFile(path);
                DragAndDrop.AcceptDrag();
            });
        }

        // ── Autocomplete System ──────────────────────────────────────────────

        /// <summary>
        /// Filters commands matching the typed prefix and shows the autocomplete popup.
        /// </summary>
        /// <param name="text">The current input field text, expected to start with '/'.</param>
        void UpdateAutocomplete(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '/' || text.Contains(' '))
            {
                HideAutocomplete();
                return;
            }

            var prefix = text.Substring(1);
            _autocompleteMatches = _commands.Match(prefix);

            if (_autocompleteMatches.Count == 0)
            {
                HideAutocomplete();
                return;
            }

            _autocompleteSelection = 0;
            RebuildAutocompleteItems();
            _autocompletePopup.style.display = DisplayStyle.Flex;
            _autocompleteVisible = true;
        }

        /// <summary>
        /// Rebuilds the autocomplete popup rows from the current <see cref="_autocompleteMatches"/> list.
        /// </summary>
        void RebuildAutocompleteItems()
        {
            _autocompletePopup.Clear();

            for (int i = 0; i < _autocompleteMatches.Count && i < 10; i++)
            {
                var cmd = _autocompleteMatches[i];
                var idx = i;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;
                row.style.paddingLeft = 10;
                row.style.paddingRight = 10;

                if (i == _autocompleteSelection)
                    row.style.backgroundColor = _theme.AutocompleteSel;

                var nameLabel = new Label($"/{cmd.Name}");
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.fontSize = _theme.FontSize(FontTier.Meta);
                nameLabel.style.width = 140;
                nameLabel.style.color = _theme.TextColor;
                row.Add(nameLabel);

                var descLabel = new Label(cmd.Description);
                descLabel.style.fontSize = _theme.FontSize(FontTier.Meta);
                descLabel.style.color = _theme.IsDark
                    ? new Color(0.55f, 0.55f, 0.58f)
                    : new Color(0.45f, 0.45f, 0.48f);
                descLabel.style.flexGrow = 1;
                descLabel.style.overflow = Overflow.Hidden;
                row.Add(descLabel);

                if (cmd.Source == CommandSource.Cli)
                {
                    var badge = new Label("CLI");
                    badge.style.fontSize = _theme.FontSize(FontTier.Hint);
                    badge.style.color = _theme.IsDark
                        ? new Color(0.45f, 0.55f, 0.70f)
                        : new Color(0.3f, 0.4f, 0.6f);
                    badge.style.marginLeft = 6;
                    badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                    row.Add(badge);
                }

                row.RegisterCallback<ClickEvent>(_ =>
                {
                    AcceptAutocomplete(idx);
                });

                _autocompletePopup.Add(row);
            }
        }

        /// <summary>
        /// Accepts the selected autocomplete item. Commands with arg choices
        /// transition to showing those choices. Commands without args execute immediately.
        /// </summary>
        /// <param name="index">Index into <see cref="_autocompleteMatches"/>.</param>
        void AcceptAutocomplete(int index)
        {
            if (index < 0 || index >= _autocompleteMatches.Count) return;

            var cmd = _autocompleteMatches[index];
            HideAutocomplete();

            if (cmd.ArgChoices != null && cmd.ArgChoices.Count > 0)
            {
                _chatInputField.Text = $"/{cmd.Name} ";
                _chatInputField.Focus();
                ShowArgChoices(cmd, "");
            }
            else if (cmd.AcceptsArgs)
            {
                _chatInputField.Text = $"/{cmd.Name} ";
                _chatInputField.Focus();
            }
            else
            {
                _chatInputField.Text = $"/{cmd.Name}";
                RaiseTrySend();
            }
        }

        /// <summary>
        /// Accepts the selected arg choice — completes the command and submits it.
        /// </summary>
        /// <param name="index">Index into <see cref="_argChoiceMatches"/>.</param>
        void AcceptArgChoice(int index)
        {
            if (_argChoiceCommand == null || index < 0 || index >= _argChoiceMatches.Count) return;

            var choice = _argChoiceMatches[index];
            _chatInputField.Text = $"/{_argChoiceCommand.Name} {choice.Value}";
            HideAutocomplete();
            RaiseTrySend();
        }

        /// <summary>
        /// Transitions the autocomplete popup into arg-choice mode for the given command,
        /// filtering choices by the provided prefix.
        /// </summary>
        /// <param name="cmd">The command whose arg choices should be displayed.</param>
        /// <param name="filter">Optional prefix to filter choices (case-insensitive).</param>
        void ShowArgChoices(SlashCommand cmd, string filter)
        {
            _argChoiceCommand = cmd;

            if (string.IsNullOrEmpty(filter))
            {
                _argChoiceMatches = new List<ArgChoice>(cmd.ArgChoices);
            }
            else
            {
                _argChoiceMatches = cmd.ArgChoices
                    .FindAll(a => a.Value.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ||
                                  a.Label.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
            }

            if (_argChoiceMatches.Count == 0)
            {
                HideAutocomplete();
                return;
            }

            _argChoiceSelection = 0;
            RebuildArgChoiceItems();
            _autocompletePopup.style.display = DisplayStyle.Flex;
            _autocompleteVisible = true;
        }

        /// <summary>
        /// Rebuilds the autocomplete popup rows from the current <see cref="_argChoiceMatches"/> list.
        /// </summary>
        void RebuildArgChoiceItems()
        {
            _autocompletePopup.Clear();

            for (int i = 0; i < _argChoiceMatches.Count; i++)
            {
                var choice = _argChoiceMatches[i];
                var idx = i;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;
                row.style.paddingLeft = 10;
                row.style.paddingRight = 10;

                if (i == _argChoiceSelection)
                    row.style.backgroundColor = _theme.AutocompleteSel;

                var nameLabel = new Label(choice.Label);
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.fontSize = _theme.FontSize(FontTier.Meta);
                nameLabel.style.width = 140;
                nameLabel.style.color = _theme.TextColor;
                row.Add(nameLabel);

                if (!string.IsNullOrEmpty(choice.Description))
                {
                    var descLabel = new Label(choice.Description);
                    descLabel.style.fontSize = _theme.FontSize(FontTier.Meta);
                    descLabel.style.color = _theme.IsDark
                        ? new Color(0.55f, 0.55f, 0.58f)
                        : new Color(0.45f, 0.45f, 0.48f);
                    descLabel.style.flexGrow = 1;
                    row.Add(descLabel);
                }

                row.RegisterCallback<ClickEvent>(_ => AcceptArgChoice(idx));
                _autocompletePopup.Add(row);
            }
        }

        /// <summary>
        /// Hides the autocomplete popup and resets all autocomplete state.
        /// </summary>
        void HideAutocomplete()
        {
            _autocompletePopup.style.display = DisplayStyle.None;
            _autocompleteMatches.Clear();
            _autocompleteSelection = -1;
            _autocompleteVisible = false;
            _argChoiceCommand = null;
            _argChoiceMatches.Clear();
            _argChoiceSelection = -1;
        }
    }
}
