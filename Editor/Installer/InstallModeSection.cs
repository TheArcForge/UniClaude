using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Renders the "Install Mode" block inside the Settings tab.
    /// Shows current mode, conversion buttons, and a Delete button.
    /// Reads fresh probe state on every Refresh() call.
    /// </summary>
    public class InstallModeSection : VisualElement
    {
        const string PackageName = "com.arcforge.uniclaude";

        readonly string _projectRoot;
        InstallModeProbe.Status _status;

        /// <summary>Create a new section.</summary>
        public InstallModeSection()
        {
            _projectRoot = Path.GetDirectoryName(Application.dataPath);
            style.marginTop = 12;
            style.marginBottom = 12;
            Refresh();
        }

        /// <summary>Re-probe state and rebuild the UI.</summary>
        public void Refresh()
        {
            Clear();
            _status = InstallModeProbe.Probe(_projectRoot);

            Add(ModeLine());
            Add(Description());

            if (_status.Mode == InstallMode.Other)
            {
                Add(new Label("Custom install \u2014 conversion not available.") { style = { marginTop = 6 } });
                return;
            }

            if (!string.IsNullOrEmpty(_status.BlockingReason))
            {
                var warn = new Label(_status.BlockingReason) { style = { marginTop = 6, color = new Color(0.8f, 0.5f, 0.2f) } };
                Add(warn);
            }

            Add(ConvertButton());
            Add(DeleteButton());
        }

        Label ModeLine()
        {
            var text = _status.Mode switch
            {
                InstallMode.Standard => "Currently: Standard mode",
                InstallMode.Ninja => "Currently: Ninja mode",
                _ => "Currently: Other",
            };
            return new Label(text) { style = { marginBottom = 4 } };
        }

        Label Description()
        {
            var text = _status.Mode switch
            {
                InstallMode.Standard =>
                    "Installed via Package Manager. Adding or removing shows up in " +
                    "Packages/manifest.json and packages-lock.json diffs.",
                InstallMode.Ninja =>
                    "UniClaude lives in Packages/com.arcforge.uniclaude/ but is hidden " +
                    "via .git/info/exclude and a git clean/smudge filter on packages-lock.json. " +
                    "git status stays clean.",
                _ => "",
            };
            return new Label(text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 } };
        }

        Button ConvertButton()
        {
            var (label, action) = _status.Mode switch
            {
                InstallMode.Standard => ("Convert to Ninja Mode", (Action)ConvertToNinja),
                InstallMode.Ninja => ("Convert to Standard Mode", (Action)ConvertToStandard),
                _ => ("", (Action)(() => { })),
            };

            var btn = new Button(action) { text = label };
            btn.SetEnabled(string.IsNullOrEmpty(_status.BlockingReason));
            btn.style.marginTop = 4;
            return btn;
        }

        Button DeleteButton()
        {
            var btn = new Button(DeleteUniClaude) { text = "Delete UniClaude" };
            btn.style.marginTop = 4;
            return btn;
        }

        void ConvertToNinja()
        {
            var confirm = EditorUtility.DisplayDialog(
                "Convert to Ninja Mode",
                "This will:\n" +
                "\u2022 Clone UniClaude into Packages/com.arcforge.uniclaude/\n" +
                "\u2022 Hide it from git via .git/info/exclude + clean/smudge filter\n" +
                "\u2022 Remove the UniClaude entry from Packages/manifest.json\n\n" +
                "git status will show no diffs. Team won't see UniClaude.\n\n" +
                "Continue?",
                "Convert", "Cancel");
            if (!confirm) return;

            var gitUrl = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(InstallModeSection).Assembly)?.packageId;
            gitUrl = ExtractGitUrl(gitUrl);

            var outcome = InstallerBridge.Run(
                InstallerBridge.Subcommand.ToNinja,
                _projectRoot, gitUrl,
                InstallerBridge.FindInstallerPath(_projectRoot));

            if (outcome.ExitCode != 0 || outcome.Status?.Result != "ok")
            {
                EditorUtility.DisplayDialog("Conversion failed",
                    outcome.Status?.Error ?? outcome.Stderr ?? "Unknown error",
                    "OK");
                Refresh();
                return;
            }

            UnityEditor.PackageManager.Client.Resolve();
        }

        void ConvertToStandard()
        {
            var confirm = EditorUtility.DisplayDialog(
                "Convert to Standard Mode",
                "This will:\n" +
                "\u2022 Uninstall the git clean/smudge filter\n" +
                "\u2022 Restore the UniClaude entry in Packages/manifest.json\n" +
                "\u2022 Quit Unity, delete the embedded package folder, and reopen Unity\n\n" +
                "A progress window will show each step and persist across the restart.\n\n" +
                "Continue?",
                "Convert", "Cancel");
            if (!confirm) return;

            InstallerBridge.StageAndExit(
                InstallerBridge.Subcommand.ToStandard,
                _projectRoot,
                InstallerBridge.FindInstallerPath(_projectRoot));
        }

        void DeleteUniClaude()
        {
            var confirm = EditorUtility.DisplayDialog(
                "Delete UniClaude",
                "This removes UniClaude from the project. Continue?",
                "Delete", "Cancel");
            if (!confirm) return;

            if (_status.Mode == InstallMode.Standard)
            {
                UnityEditor.PackageManager.Client.Remove(PackageName);
                return;
            }

            if (_status.Mode == InstallMode.Ninja)
            {
                InstallerBridge.StageAndExit(
                    InstallerBridge.Subcommand.DeleteFromNinja,
                    _projectRoot,
                    InstallerBridge.FindInstallerPath(_projectRoot));
            }
        }

        static string ExtractGitUrl(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return null;
            var at = packageId.IndexOf('@');
            return at < 0 ? null : packageId.Substring(at + 1);
        }
    }
}
