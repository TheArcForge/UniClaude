using System.IO;
using UnityEditor;
using UnityEngine;

namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Runs on every editor load. If a pending transition marker exists in
    /// EditorPrefs, reopens <see cref="TransitionProgressWindow"/> in resume
    /// mode to show the terminal state of the helper that ran while Unity
    /// was shut down.
    /// </summary>
    [InitializeOnLoad]
    public static class InstallerPostReload
    {
        static InstallerPostReload()
        {
            EditorApplication.delayCall += ResumeIfPending;
        }

        static void ResumeIfPending()
        {
            var markerPath = EditorPrefs.GetString(
                TransitionProgressWindow.PendingMarkerPathPrefKey, "");
            if (string.IsNullOrEmpty(markerPath)) return;

            if (!File.Exists(markerPath))
            {
                Debug.LogWarning(
                    $"[UniClaude] Pending-transition marker missing at {markerPath}; clearing EditorPrefs.");
                EditorPrefs.DeleteKey(TransitionProgressWindow.PendingMarkerPathPrefKey);
                return;
            }

            TransitionProgressWindow.ResumeFrom(markerPath);
        }
    }
}
