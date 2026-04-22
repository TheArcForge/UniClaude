using System;
using System.IO;
using System.Threading.Tasks;
using UniClaude.Editor.Installer;
using UnityEditor;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>
    /// Ninja-mode updater: fetches tags and checks out a specific release tag in the embedded
    /// Packages/com.arcforge.uniclaude clone. Refuses to run on a dirty working tree.
    /// </summary>
    public static class NinjaUpdater
    {
        const string PackageDir = "Packages/com.arcforge.uniclaude";

        /// <summary>Result of a ninja update.</summary>
        public struct Result
        {
            /// <summary>True when fetch and checkout both succeeded.</summary>
            public bool Ok;
            /// <summary>Error message when <see cref="Ok"/> is false.</summary>
            public string ErrorMessage;
        }

        /// <summary>
        /// Pre-flight: dirty-tree check. Returns null on clean, error message on dirty/unreachable.
        /// </summary>
        /// <param name="projectRoot">Unity project root.</param>
        /// <returns>Null if safe to proceed, otherwise a user-facing error message.</returns>
        public static string PreflightCheck(string projectRoot)
        {
            var pkgPath = Path.Combine(projectRoot, PackageDir);
            if (!Directory.Exists(pkgPath))
                return "Package folder not found at " + PackageDir;

            var status = GitCli.Run(pkgPath, "status", "--porcelain");
            if (status.ExitCode != 0)
                return "git status failed: " + status.Stderr;
            if (!string.IsNullOrWhiteSpace(status.Stdout))
                return "The UniClaude folder has local changes. Commit, stash, or discard them before updating.";
            return null;
        }

        /// <summary>
        /// Run fetch + checkout on a background thread.
        /// Caller is responsible for opening/closing the progress window.
        /// </summary>
        /// <param name="projectRoot">Unity project root.</param>
        /// <param name="tag">Target tag (e.g. "v0.3.0").</param>
        /// <returns>Update result.</returns>
        public static Task<Result> UpdateAsync(string projectRoot, string tag)
        {
            return Task.Run(() =>
            {
                var pkgPath = Path.Combine(projectRoot, PackageDir);

                var fetch = GitCli.Run(pkgPath, "fetch", "--tags", "origin");
                if (fetch.ExitCode != 0)
                    return new Result { Ok = false, ErrorMessage = "fetch failed: " + fetch.Stderr };

                var checkout = GitCli.Run(pkgPath, "checkout", tag);
                if (checkout.ExitCode != 0)
                    return new Result { Ok = false, ErrorMessage = "checkout failed: " + checkout.Stderr };

                return new Result { Ok = true };
            });
        }

        /// <summary>
        /// Entry point invoked from the UI. Runs preflight, shows progress, performs update,
        /// and refreshes the AssetDatabase on success.
        /// </summary>
        /// <param name="projectRoot">Unity project root.</param>
        /// <param name="tag">Target tag.</param>
        public static async void RunInteractive(string projectRoot, string tag)
        {
            var preflight = PreflightCheck(projectRoot);
            if (preflight != null)
            {
                EditorUtility.DisplayDialog("Cannot update UniClaude", preflight, "OK");
                return;
            }

            var window = EditorWindow.GetWindow<NinjaUpdateProgressWindow>(utility: true, title: "Updating UniClaude", focus: true);
            window.Begin(tag);

            var result = await UpdateAsync(projectRoot, tag);
            window.Finish(result);

            if (result.Ok)
            {
                AssetDatabase.Refresh();
            }
        }
    }

    /// <summary>
    /// Minimal progress window for a ninja update. Separate from the install-flow
    /// <c>TransitionProgressWindow</c>, which is specific to conversion/deletion.
    /// </summary>
    public class NinjaUpdateProgressWindow : EditorWindow
    {
        string _tag;
        string _state = "Starting…";
        NinjaUpdater.Result? _result;

        /// <summary>Initialize the window with the tag being applied.</summary>
        /// <param name="tag">Target tag name.</param>
        public void Begin(string tag)
        {
            _tag = tag;
            _state = "Fetching and checking out " + tag + "…";
            Repaint();
        }

        /// <summary>Display the terminal state of the update.</summary>
        /// <param name="result">Outcome of the update.</param>
        public void Finish(NinjaUpdater.Result result)
        {
            _result = result;
            _state = result.Ok
                ? "Updated to " + _tag + " — Unity is recompiling."
                : "Update failed: " + result.ErrorMessage;
            Repaint();
        }

        void OnGUI()
        {
            UnityEngine.GUILayout.Space(8);
            UnityEngine.GUILayout.Label(_state, EditorStyles.wordWrappedLabel);
            UnityEngine.GUILayout.Space(8);

            if (_result.HasValue)
            {
                if (UnityEngine.GUILayout.Button("Close"))
                    Close();
            }
        }
    }
}
