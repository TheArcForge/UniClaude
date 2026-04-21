using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Runs on every editor load. If a transition checkpoint is pending,
    /// verifies the expected post-reload state and surfaces success or recovery UI.
    /// </summary>
    [InitializeOnLoad]
    public static class InstallerPostReload
    {
        static FileSystemWatcher _watcher;
        static bool _watcherFired;

        static InstallerPostReload()
        {
            EditorApplication.delayCall += Verify;
        }

        static void Verify()
        {
            var key = InstallerBridge.ReadCheckpoint();
            if (string.IsNullOrEmpty(key)) return;

            var age = System.DateTime.UtcNow - InstallerBridge.ReadCheckpointTimestamp();
            if (age > System.TimeSpan.FromMinutes(5))
            {
                var reset = EditorUtility.DisplayDialog(
                    "UniClaude transition stuck",
                    $"A transition ({key}) has been pending for {(int)age.TotalMinutes} minutes. " +
                    "Clear the checkpoint and review install state manually?",
                    "Clear", "Keep waiting");
                if (reset)
                {
                    InstallerBridge.WriteCheckpoint("");
                    return;
                }
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var status = InstallModeProbe.Probe(projectRoot);

            switch (key)
            {
                case "to-ninja:pending":
                    HandleToNinja(status);
                    break;
                case "to-standard:phase1":
                    HandleToStandard(status, projectRoot);
                    break;
                case "delete-from-ninja:pending":
                    HandleDeleteFromNinja(status, projectRoot);
                    break;
                default:
                    Debug.LogWarning($"[UniClaude] Unknown transition checkpoint: {key}");
                    InstallerBridge.WriteCheckpoint("");
                    break;
            }
        }

        static void HandleToNinja(InstallModeProbe.Status status)
        {
            if (status.Mode == InstallMode.Ninja)
            {
                InstallerBridge.WriteCheckpoint("");
                Debug.Log("[UniClaude] Ninja mode active. git status should be clean.");
                return;
            }
            Debug.LogError("[UniClaude] Conversion to Ninja did not complete. See Settings \u2192 Install Mode for recovery.");
        }

        static void HandleToStandard(InstallModeProbe.Status status, string projectRoot)
        {
            if (status.Mode == InstallMode.Standard)
            {
                InstallerBridge.WriteCheckpoint("");
                Debug.Log("[UniClaude] Standard mode active.");
                return;
            }

            Debug.Log("[UniClaude] Awaiting deletion of embedded package (detached deleter running)...");
            InstallDeletionWatcher(projectRoot, () =>
            {
                UnityEditor.PackageManager.Client.Resolve();
            });
        }

        static void HandleDeleteFromNinja(InstallModeProbe.Status status, string projectRoot)
        {
            var pkgPath = Path.Combine(projectRoot, "Packages", "com.arcforge.uniclaude");
            if (!Directory.Exists(pkgPath))
            {
                InstallerBridge.WriteCheckpoint("");
                Debug.Log("[UniClaude] UniClaude removed.");
                return;
            }
            Debug.Log("[UniClaude] Awaiting deletion of embedded package...");
            InstallDeletionWatcher(projectRoot, () =>
            {
                UnityEditor.PackageManager.Client.Resolve();
            });
        }

        /// <summary>Install a one-shot watcher that fires onComplete when the detached deleter writes transition-status.json.</summary>
        /// <param name="projectRoot">The Unity project root directory.</param>
        /// <param name="onComplete">Action to invoke on the main thread when the status file appears.</param>
        static void InstallDeletionWatcher(string projectRoot, System.Action onComplete)
        {
            _watcher?.Dispose();
            _watcherFired = false;
            var dir = Path.Combine(projectRoot, "Library", "UniClaude");
            Directory.CreateDirectory(dir);
            _watcher = new FileSystemWatcher(dir, "transition-status.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, __) => EditorApplication.delayCall += () =>
            {
                if (_watcherFired) return;
                _watcherFired = true;
                _watcher?.Dispose();
                _watcher = null;
                onComplete?.Invoke();
            };
        }
    }
}
