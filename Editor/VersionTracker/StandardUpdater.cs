using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;

namespace UniClaude.Editor.VersionTracker
{
    /// <summary>
    /// Standard-mode updater: rewrites the tag in Packages/manifest.json and triggers Unity's
    /// Package Manager to resolve. Unity owns the progress UI after that.
    /// </summary>
    public static class StandardUpdater
    {
        const string PackageName = "com.arcforge.uniclaude";

        /// <summary>Outcome of an update attempt.</summary>
        public struct Result
        {
            /// <summary>True when the manifest was rewritten and Client.Resolve was triggered.</summary>
            public bool Ok;
            /// <summary>User-facing result message.</summary>
            public string Message;
            /// <summary>True when refusal is due to floating ref (not a failure — informational).</summary>
            public bool IsFloatingRef;
        }

        /// <summary>Classify the manifest entry so the UI can enable/disable the button.</summary>
        /// <param name="projectRoot">Unity project root.</param>
        /// <returns>The detected entry kind.</returns>
        public static ManifestEditor.EntryKind GetEntryKind(string projectRoot)
        {
            var path = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(path)) return ManifestEditor.EntryKind.Missing;
            try
            {
                var json = File.ReadAllText(path);
                return ManifestEditor.Inspect(json, PackageName).Kind;
            }
            catch
            {
                return ManifestEditor.EntryKind.Missing;
            }
        }

        /// <summary>
        /// Rewrite the tag in manifest.json and call <see cref="Client.Resolve"/>.
        /// Does not block on resolution — the Package Manager handles progress UI.
        /// </summary>
        /// <param name="projectRoot">Unity project root.</param>
        /// <param name="newTag">Target tag (e.g. "v0.3.0").</param>
        /// <returns>Update result.</returns>
        public static Result Update(string projectRoot, string newTag)
        {
            var path = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(path))
                return new Result { Ok = false, Message = "manifest.json not found" };

            string original;
            try { original = File.ReadAllText(path); }
            catch (Exception ex) { return new Result { Ok = false, Message = "Read failed: " + ex.Message }; }

            var insp = ManifestEditor.Inspect(original, PackageName);
            if (insp.Kind == ManifestEditor.EntryKind.Missing)
                return new Result { Ok = false, Message = "UniClaude not present in manifest.json" };
            if (insp.Kind == ManifestEditor.EntryKind.Floating)
                return new Result
                {
                    Ok = false,
                    IsFloatingRef = true,
                    Message = "UniClaude is tracking a floating ref. Update manually by editing manifest.json.",
                };

            string updated;
            try { updated = ManifestEditor.RewriteTag(original, PackageName, newTag); }
            catch (Exception ex) { return new Result { Ok = false, Message = "Rewrite failed: " + ex.Message }; }

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, updated);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            Client.Resolve();

            return new Result { Ok = true, Message = "Updated to " + newTag + " — Package Manager is resolving." };
        }
    }
}
