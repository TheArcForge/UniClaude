using System;
using UniClaude.Editor.Installer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>
    /// Settings-tab section that displays the current version, update status, and update controls.
    /// Four visual states: Up to date, Checking, Update available, Check failed.
    /// </summary>
    public class VersionTrackerSection : VisualElement
    {
        readonly VersionCheckService _service;
        readonly string _currentVersion;
        readonly string _projectRoot;

        Label _statusLabel;
        Button _primaryButton;     // "Update now" / "Retry" / "Check now"
        Button _secondaryButton;   // "View changes"
        VisualElement _releaseNotesPanel;
        Label _toastLabel;
        bool _releaseNotesExpanded;
        CheckResult _cachedResult;

        /// <summary>Create a section with the given service and environment.</summary>
        /// <param name="service">Check service (already configured with a fetcher).</param>
        /// <param name="currentVersion">Current package version string from package.json.</param>
        /// <param name="projectRoot">Unity project root for updater file operations.</param>
        public VersionTrackerSection(VersionCheckService service, string currentVersion, string projectRoot)
        {
            _service = service;
            _currentVersion = currentVersion;
            _projectRoot = projectRoot;

            style.marginBottom = 12;
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.paddingLeft = 12;
            style.paddingRight = 12;
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.05f);

            BuildLayout();
            RenderFromCache();
            MaybeRefreshInBackground();
        }

        void BuildLayout()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            _statusLabel = new Label();
            _statusLabel.style.flexGrow = 1;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(_statusLabel);

            _secondaryButton = new Button(() => { });
            _secondaryButton.style.marginRight = 6;
            _secondaryButton.style.display = DisplayStyle.None;
            row.Add(_secondaryButton);

            _primaryButton = new Button(() => { });
            row.Add(_primaryButton);

            Add(row);

            _releaseNotesPanel = new VisualElement();
            _releaseNotesPanel.style.display = DisplayStyle.None;
            _releaseNotesPanel.style.marginTop = 8;
            _releaseNotesPanel.style.paddingTop = 8;
            _releaseNotesPanel.style.maxHeight = 200;
            _releaseNotesPanel.style.overflow = Overflow.Hidden;
            Add(_releaseNotesPanel);

            _toastLabel = new Label();
            _toastLabel.style.display = DisplayStyle.None;
            _toastLabel.style.marginTop = 8;
            _toastLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(_toastLabel);
        }

        void RenderFromCache() => Render(_service.GetCached());

        async void MaybeRefreshInBackground()
        {
            var settings = UniClaudeSettings.Load();
            if (VersionCheckService.IsCacheFresh(settings, DateTime.UtcNow)) return;

            RenderChecking();
            var r = await _service.CheckAsync(force: false);
            Render(r);
        }

        void RenderChecking()
        {
            _statusLabel.text = "Checking for updates…";
            _primaryButton.style.display = DisplayStyle.None;
            _secondaryButton.style.display = DisplayStyle.None;
            _releaseNotesPanel.style.display = DisplayStyle.None;
        }

        void Render(CheckResult r)
        {
            _releaseNotesPanel.Clear();
            _releaseNotesPanel.style.display = DisplayStyle.None;
            _releaseNotesExpanded = false;

            switch (r.Status)
            {
                case CheckStatus.UpToDate:
                case CheckStatus.Unknown:
                    _statusLabel.text = $"UniClaude v{_currentVersion} · Up to date";
                    _primaryButton.text = "Check now";
                    _primaryButton.style.display = DisplayStyle.Flex;
                    _primaryButton.SetEnabled(true);
                    _primaryButton.tooltip = "";
                    _primaryButton.clicked -= ForceCheck;
                    _primaryButton.clicked -= StartUpdate;
                    _primaryButton.clicked += ForceCheck;
                    _secondaryButton.style.display = DisplayStyle.None;
                    break;

                case CheckStatus.UpdateAvailable:
                    _statusLabel.text = $"Update available: {r.LatestVersion} (current: v{_currentVersion})";
                    _primaryButton.text = "Update now";
                    _primaryButton.style.display = DisplayStyle.Flex;
                    _primaryButton.clicked -= ForceCheck;
                    _primaryButton.clicked -= StartUpdate;
                    _primaryButton.clicked += StartUpdate;

                    _secondaryButton.text = "View changes";
                    _secondaryButton.style.display = DisplayStyle.Flex;
                    _secondaryButton.clicked -= ToggleReleaseNotes;
                    _secondaryButton.clicked += ToggleReleaseNotes;

                    _cachedResult = r;
                    ConfigureUpdateAvailabilityForMode(r);
                    break;

                case CheckStatus.Failed:
                    _statusLabel.text = "Couldn't check for updates (" + (r.ErrorMessage ?? "unknown") + ")";
                    _primaryButton.text = "Retry";
                    _primaryButton.style.display = DisplayStyle.Flex;
                    _primaryButton.SetEnabled(true);
                    _primaryButton.tooltip = "";
                    _primaryButton.clicked -= ForceCheck;
                    _primaryButton.clicked -= StartUpdate;
                    _primaryButton.clicked += ForceCheck;
                    _secondaryButton.style.display = DisplayStyle.None;
                    break;
            }
        }

        void ConfigureUpdateAvailabilityForMode(CheckResult r)
        {
            var probe = InstallModeProbe.Probe(_projectRoot);
            if (probe.Mode == InstallMode.Standard)
            {
                var kind = StandardUpdater.GetEntryKind(_projectRoot);
                if (kind == ManifestEditor.EntryKind.Floating)
                {
                    _primaryButton.SetEnabled(false);
                    _primaryButton.tooltip =
                        "UniClaude is tracking a floating git ref. Update manually by editing Packages/manifest.json.";
                    return;
                }
            }
            else if (probe.Mode != InstallMode.Ninja)
            {
                _primaryButton.SetEnabled(false);
                _primaryButton.tooltip =
                    "UniClaude is installed in an unsupported mode. Update manually at " + (r.ReleaseUrl ?? "github.com");
                return;
            }

            _primaryButton.SetEnabled(true);
            _primaryButton.tooltip = "";
        }

        void ToggleReleaseNotes()
        {
            _releaseNotesExpanded = !_releaseNotesExpanded;
            _releaseNotesPanel.style.display = _releaseNotesExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _secondaryButton.text = _releaseNotesExpanded ? "Hide changes" : "View changes";

            if (_releaseNotesExpanded && _releaseNotesPanel.childCount == 0 && _cachedResult != null)
            {
                var notes = new Label(_cachedResult.ReleaseNotesMarkdown ?? "(no release notes)");
                notes.style.whiteSpace = WhiteSpace.Normal;
                _releaseNotesPanel.Add(notes);

                if (!string.IsNullOrEmpty(_cachedResult.ReleaseUrl))
                {
                    var link = new Button(() => Application.OpenURL(_cachedResult.ReleaseUrl))
                        { text = "Open on GitHub" };
                    link.style.alignSelf = Align.FlexStart;
                    link.style.marginTop = 6;
                    _releaseNotesPanel.Add(link);
                }
            }
        }

        async void ForceCheck()
        {
            RenderChecking();
            var r = await _service.CheckAsync(force: true);
            Render(r);
        }

        void StartUpdate()
        {
            if (_cachedResult == null || string.IsNullOrEmpty(_cachedResult.LatestVersion)) return;
            var tag = _cachedResult.LatestVersion;

            if (!EditorUtility.DisplayDialog(
                "Update UniClaude to " + tag + "?",
                "This will change the installed version. Unity may reload.",
                "Update", "Cancel"))
                return;

            var probe = InstallModeProbe.Probe(_projectRoot);
            if (probe.Mode == InstallMode.Ninja)
            {
                NinjaUpdater.RunInteractive(_projectRoot, tag);
            }
            else if (probe.Mode == InstallMode.Standard)
            {
                var result = StandardUpdater.Update(_projectRoot, tag);
                ShowToast(result.Message);
            }
        }

        void ShowToast(string message)
        {
            _toastLabel.text = message;
            _toastLabel.style.display = DisplayStyle.Flex;
            schedule.Execute(() =>
            {
                _toastLabel.style.display = DisplayStyle.None;
            }).StartingIn(5000);
        }
    }
}
