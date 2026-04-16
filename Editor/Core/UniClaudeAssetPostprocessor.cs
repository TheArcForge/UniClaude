using UnityEditor;

namespace UniClaude.Editor
{
    /// <summary>
    /// Watches for asset changes and triggers incremental index updates
    /// when a <see cref="ProjectAwareness"/> instance is active.
    /// </summary>
    public class UniClaudeAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Called by Unity after assets are imported, deleted, or moved.
        /// Forwards changes to the active ProjectAwareness instance if one exists.
        /// </summary>
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ProjectAwareness.Instance == null)
                return;

            ProjectAwareness.Instance.HandleAssetsChanged(
                importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}
