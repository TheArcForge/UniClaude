using System.IO;
using System.Linq;
using UnityEditor;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for querying, searching, moving, and reimporting Unity assets
    /// via the AssetDatabase API.
    /// </summary>
    public static class AssetTools
    {
        /// <summary>
        /// Returns metadata for an asset at the given path, including its type, GUID,
        /// labels, and direct dependencies.
        /// </summary>
        /// <param name="path">Asset path relative to the project root (e.g. "Assets/Scripts/Player.cs").</param>
        /// <returns>Asset type, GUID, labels, and dependencies on success; error if the asset is not found.</returns>
        [MCPTool("asset_get_info", "Get asset metadata: type, GUID, labels, and direct dependencies. " +
            "If the returned type is 'UnityEditor.DefaultAsset', Unity has no native importer for this format — " +
            "common unsupported formats: .webp, .svg, .heic. Convert to PNG/JPG/TGA/PSD/EXR before use.")]
        public static MCPToolResult GetAssetInfo(
            [MCPToolParam("Asset path relative to project root (e.g. 'Assets/Scripts/Player.cs')", required: true)]
            string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == null)
                return MCPToolResult.Error($"Asset not found at path: {path}");

            var guid = AssetDatabase.GUIDFromAssetPath(path);
            var labels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path));
            var dependencies = AssetDatabase.GetDependencies(path, false);

            return MCPToolResult.Success(new
            {
                path,
                type = type.FullName,
                guid = guid.ToString(),
                labels,
                dependencies
            });
        }

        /// <summary>
        /// Searches for assets matching a filter string, optionally restricted to specific folders.
        /// Results are capped at 100 paths.
        /// </summary>
        /// <param name="filter">AssetDatabase search filter (e.g. "t:Script", "t:Texture player").</param>
        /// <param name="searchInFolders">Optional comma-separated folder paths to restrict the search (e.g. "Assets/Scripts,Assets/Prefabs").</param>
        /// <returns>An array of matching asset paths, capped at 100.</returns>
        [MCPTool("asset_find", "Search for assets by filter (e.g. 't:Script', 't:Texture player'). Optional folder restriction. Max 100 results.")]
        public static MCPToolResult FindAssets(
            [MCPToolParam("AssetDatabase search filter (e.g. 't:Script', 't:Texture player')", required: true)]
            string filter,
            [MCPToolParam("Comma-separated folder paths to search in (e.g. 'Assets/Scripts,Assets/Prefabs')")]
            string searchInFolders)
        {
            string[] folders = null;
            if (!string.IsNullOrEmpty(searchInFolders))
            {
                folders = searchInFolders
                    .Split(',')
                    .Select(f => f.Trim())
                    .Where(f => f.Length > 0)
                    .ToArray();
            }

            var guids = folders != null && folders.Length > 0
                ? AssetDatabase.FindAssets(filter, folders)
                : AssetDatabase.FindAssets(filter);

            var paths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Take(100)
                .ToArray();

            return MCPToolResult.Success(new
            {
                filter,
                matches = paths,
                count = paths.Length,
                truncated = guids.Length > 100
            });
        }

        /// <summary>
        /// Moves an asset from one path to another. Creates the target directory if it
        /// does not exist. Uses <see cref="AssetDatabase.MoveAsset"/> internally.
        /// </summary>
        /// <param name="sourcePath">Current asset path (e.g. "Assets/Old/Player.cs").</param>
        /// <param name="destPath">Destination asset path (e.g. "Assets/New/Player.cs").</param>
        /// <returns>Success with the new path, or an error describing the failure.</returns>
        [MCPTool("asset_move", "Move or rename an asset from one path to another")]
        public static MCPToolResult MoveAsset(
            [MCPToolParam("Current asset path (e.g. 'Assets/Old/Player.cs')", required: true)]
            string sourcePath,
            [MCPToolParam("Destination asset path (e.g. 'Assets/New/Player.cs')", required: true)]
            string destPath)
        {
            var sourceType = AssetDatabase.GetMainAssetTypeAtPath(sourcePath);
            if (sourceType == null)
                return MCPToolResult.Error($"Source asset not found at path: {sourcePath}");

            // Ensure the destination directory exists
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !AssetDatabase.IsValidFolder(destDir))
            {
                CreateFolderRecursive(destDir);
            }

            var error = AssetDatabase.MoveAsset(sourcePath, destPath);
            if (!string.IsNullOrEmpty(error))
                return MCPToolResult.Error($"Move failed: {error}");

            return MCPToolResult.Success(new
            {
                source = sourcePath,
                destination = destPath
            });
        }

        /// <summary>
        /// Forces a reimport of the asset at the specified path using
        /// <see cref="ImportAssetOptions.ForceUpdate"/>.
        /// </summary>
        /// <param name="path">Asset path to reimport (e.g. "Assets/Textures/icon.png").</param>
        /// <returns>Success confirmation, or an error if the asset does not exist.</returns>
        [MCPTool("asset_import", "Force reimport of an asset at the given path")]
        public static MCPToolResult ImportAsset(
            [MCPToolParam("Asset path to reimport (e.g. 'Assets/Textures/icon.png')", required: true)]
            string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == null)
                return MCPToolResult.Error($"Asset not found at path: {path}");

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return MCPToolResult.Success(new
            {
                path,
                reimported = true
            });
        }

        // ── Helpers ──

        /// <summary>
        /// Recursively creates folder hierarchy via <see cref="AssetDatabase.CreateFolder"/>
        /// so that Unity tracks each segment properly.
        /// </summary>
        /// <param name="folderPath">The full folder path to create (e.g. "Assets/New/Deep/Folder").</param>
        static void CreateFolderRecursive(string folderPath)
        {
            // Normalize separators
            folderPath = folderPath.Replace('\\', '/');

            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }

            var folderName = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent ?? "", folderName);
        }
    }
}
