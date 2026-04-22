using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Modal-utility window showing a 4-row checklist while a conversion-deletion
    /// transition runs. Survives domain reload via EditorPrefs; reopens on restart
    /// to show the terminal state of the helper that ran while Unity was down.
    /// </summary>
    public class TransitionProgressWindow : EditorWindow
    {
        /// <summary>EditorPrefs key holding the absolute path to pending-transition.json.</summary>
        public const string PendingMarkerPathPrefKey = "UniClaude.PendingTransition";

        /// <summary>Row glyph state.</summary>
        public enum RowState
        {
            /// <summary>Not reached yet.</summary>
            Pending,
            /// <summary>Currently running (spinner).</summary>
            Active,
            /// <summary>Finished successfully.</summary>
            Complete,
            /// <summary>Finished with error.</summary>
            Failed,
        }

        /// <summary>Resolved state for each of the four checklist rows.</summary>
        public struct RowStates
        {
            public RowState Staging;
            public RowState Quitting;
            public RowState Deleting;
            public RowState Relaunching;
        }

        TransitionKind _kind;
        string _markerPath;
        string _statusPath;
        TransitionStatus _status;
        bool _showLog;
        string _logText;

        /// <summary>Public for testing: window title string for the given kind.</summary>
        public static string TitleFor(TransitionKind kind) => kind switch
        {
            TransitionKind.ToStandard => "UniClaude: Converting to Standard Mode",
            TransitionKind.DeleteFromNinja => "UniClaude: Removing UniClaude",
            _ => "UniClaude: Transition",
        };

        /// <summary>Public for testing: whether the status represents a terminal state.</summary>
        public static bool IsTerminal(TransitionStatus status)
        {
            if (status == null) return false;
            return status.Result == "ok" || status.Result == "error";
        }

        /// <summary>Public for testing: derive per-row render states from a status snapshot.</summary>
        public static RowStates ComputeRowStates(TransitionStatus status)
        {
            var rows = new RowStates
            {
                Staging = RowState.Complete,      // Always ✓ once the window exists
                Quitting = RowState.Pending,
                Deleting = RowState.Pending,
                Relaunching = RowState.Pending,
            };
            if (status == null) return rows;

            switch (status.Step)
            {
                case "staged":
                    rows.Quitting = RowState.Active;
                    break;
                case "awaiting-exit":
                    rows.Quitting = status.Result == "error" ? RowState.Failed : RowState.Active;
                    break;
                case "deleting":
                    rows.Quitting = RowState.Complete;
                    rows.Deleting = status.Result == "error" ? RowState.Failed : RowState.Active;
                    break;
                case "relaunching":
                    rows.Quitting = RowState.Complete;
                    rows.Deleting = RowState.Complete;
                    rows.Relaunching = status.Result == "error" ? RowState.Failed : RowState.Active;
                    break;
                case "complete":
                    rows.Quitting = RowState.Complete;
                    rows.Deleting = RowState.Complete;
                    rows.Relaunching = string.IsNullOrEmpty(status.RelaunchError)
                        ? RowState.Complete
                        : RowState.Failed;
                    break;
            }
            return rows;
        }

        /// <summary>Open the window for a transition just staged. Called by InstallerBridge.</summary>
        public static TransitionProgressWindow OpenForNewTransition(TransitionKind kind, string markerPath)
        {
            EditorPrefs.SetString(PendingMarkerPathPrefKey, markerPath);
            var w = GetWindow<TransitionProgressWindow>(utility: true, title: TitleFor(kind), focus: true);
            w.minSize = new Vector2(420, 260);
            w.maxSize = new Vector2(420, 260);
            w._kind = kind;
            w._markerPath = markerPath;
            var marker = PendingTransitionMarker.Read(markerPath);
            w._statusPath = marker?.StatusPath;
            w.RefreshFromDisk();
            w.Rebuild();
            return w;
        }

        /// <summary>Open the window in resume mode from a pending marker.</summary>
        public static TransitionProgressWindow ResumeFrom(string markerPath)
        {
            var marker = PendingTransitionMarker.Read(markerPath);
            if (marker == null) return null;
            var kind = TransitionKindExtensions.FromWireString(marker.Kind);
            var w = GetWindow<TransitionProgressWindow>(utility: true, title: TitleFor(kind), focus: true);
            w.minSize = new Vector2(420, 260);
            w.maxSize = new Vector2(420, 260);
            w._kind = kind;
            w._markerPath = markerPath;
            w._statusPath = marker.StatusPath;
            w.RefreshFromDisk();
            w.Rebuild();
            return w;
        }

        void RefreshFromDisk()
        {
            if (string.IsNullOrEmpty(_statusPath) || !File.Exists(_statusPath))
            {
                _status = null;
                _logText = "(no status file yet)";
                return;
            }
            _logText = File.ReadAllText(_statusPath);
            try { _status = JsonConvert.DeserializeObject<TransitionStatus>(_logText); }
            catch { _status = null; }
        }

        void CreateGUI() { Rebuild(); }

        void Rebuild()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingTop = 12;
            rootVisualElement.style.paddingBottom = 12;
            rootVisualElement.style.paddingLeft = 16;
            rootVisualElement.style.paddingRight = 16;

            var rows = ComputeRowStates(_status);
            rootVisualElement.Add(Row("Staging changes", rows.Staging, null));
            rootVisualElement.Add(Row("Quitting Unity", rows.Quitting, null));
            rootVisualElement.Add(Row("Deleting package", rows.Deleting,
                rows.Deleting == RowState.Failed ? _status?.Error : null));
            rootVisualElement.Add(Row("Relaunching Unity", rows.Relaunching,
                rows.Relaunching == RowState.Failed ? _status?.RelaunchError : null));

            var hint = new Label(_status == null || _status.Step == "staged"
                ? "Unity will close in a moment. This window will reopen when Unity restarts."
                : "");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 12;
            hint.style.color = new Color(0.7f, 0.7f, 0.7f);
            rootVisualElement.Add(hint);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            rootVisualElement.Add(spacer);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceBetween;

            var logBtn = new Button(ToggleLog) { text = _showLog ? "Hide log" : "View log" };
            buttonRow.Add(logBtn);

            var closeBtn = new Button(Close_) { text = "Close" };
            closeBtn.SetEnabled(IsTerminal(_status));
            buttonRow.Add(closeBtn);

            rootVisualElement.Add(buttonRow);

            if (_showLog)
            {
                var log = new TextField { value = _logText, multiline = true };
                log.style.marginTop = 8;
                log.style.height = 100;
                log.SetEnabled(false);
                rootVisualElement.Add(log);
            }
        }

        VisualElement Row(string label, RowState state, string error)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var glyph = new Label(GlyphFor(state));
            glyph.style.width = 20;
            glyph.style.color = ColorFor(state);
            row.Add(glyph);

            var text = new Label(label);
            text.style.color = state == RowState.Failed ? new Color(0.9f, 0.4f, 0.4f) : Color.white;
            row.Add(text);

            if (!string.IsNullOrEmpty(error))
            {
                var errLabel = new Label("  — " + error);
                errLabel.style.color = new Color(0.9f, 0.4f, 0.4f);
                errLabel.style.whiteSpace = WhiteSpace.Normal;
                row.Add(errLabel);
            }
            return row;
        }

        static string GlyphFor(RowState s) => s switch
        {
            RowState.Complete => "✓",
            RowState.Failed => "✗",
            RowState.Active => "●",
            _ => "○",
        };

        static Color ColorFor(RowState s) => s switch
        {
            RowState.Complete => new Color(0.4f, 0.8f, 0.4f),
            RowState.Failed => new Color(0.9f, 0.4f, 0.4f),
            RowState.Active => new Color(0.6f, 0.8f, 1.0f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };

        void ToggleLog()
        {
            _showLog = !_showLog;
            Rebuild();
        }

        void Close_()
        {
            EditorPrefs.DeleteKey(PendingMarkerPathPrefKey);
            try
            {
                if (!string.IsNullOrEmpty(_markerPath) && File.Exists(_markerPath))
                    File.Delete(_markerPath);
                if (!string.IsNullOrEmpty(_statusPath) && File.Exists(_statusPath))
                    File.Delete(_statusPath);
            }
            catch { /* best effort cleanup */ }
            Close();
        }
    }
}
