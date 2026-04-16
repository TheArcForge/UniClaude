using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for saving, creating, opening, duplicating scenes and managing build settings.
    /// </summary>
    public static class SceneManagementTools
    {
        /// <summary>
        /// Saves the active scene. If path is provided, performs Save As.
        /// </summary>
        /// <param name="path">Optional asset path for Save As.</param>
        /// <returns>The saved path, or error if scene was never saved and no path given.</returns>
        [MCPTool("scene_save", "Save the active scene (provide path for Save As)")]
        public static MCPToolResult SaveScene(
            [MCPToolParam("Asset path for Save As (e.g. 'Assets/Scenes/MyScene.unity'). Omit to save in place.")] string path = null)
        {
            var scene = SceneManager.GetActiveScene();

            if (string.IsNullOrEmpty(path))
            {
                if (string.IsNullOrEmpty(scene.path))
                    return MCPToolResult.Error(
                        "Scene has never been saved and no path was provided. " +
                        "Provide a path parameter (e.g. 'Assets/Scenes/MyScene.unity').");

                EditorSceneManager.SaveScene(scene);
                return MCPToolResult.Success(new { saved = scene.path });
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                EnsureFolderExists(directory);

            EditorSceneManager.SaveScene(scene, path);
            return MCPToolResult.Success(new { saved = path });
        }

        /// <summary>
        /// Creates a new scene, optionally from a template, and saves it.
        /// </summary>
        /// <param name="path">Asset path for the new scene.</param>
        /// <param name="template">Optional template scene path to copy from.</param>
        /// <returns>The created path, or error if template not found.</returns>
        [MCPTool("scene_create", "Create a new scene and save it (optionally from a template scene)")]
        public static MCPToolResult CreateScene(
            [MCPToolParam("Asset path for the new scene (e.g. 'Assets/Scenes/Level1.unity')", required: true)] string path,
            [MCPToolParam("Template scene path to copy from (omit for default scene)")] string template = null)
        {
            if (string.IsNullOrEmpty(path))
                return MCPToolResult.Error("Scene path is required.");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                EnsureFolderExists(directory);

            if (!string.IsNullOrEmpty(template))
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(template) == null)
                    return MCPToolResult.Error($"Template scene not found at '{template}'.");

                AssetDatabase.CopyAsset(template, path);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }

            return MCPToolResult.Success(new { created = path });
        }

        /// <summary>
        /// Opens a scene in the editor.
        /// </summary>
        /// <param name="path">Scene asset path.</param>
        /// <param name="additive">Open additively if "true".</param>
        /// <returns>List of loaded scenes, or error if not found.</returns>
        [MCPTool("scene_open", "Open a scene in the editor (single or additive mode)")]
        public static MCPToolResult OpenScene(
            [MCPToolParam("Scene asset path", required: true)] string path,
            [MCPToolParam("Open additively (default: false). Set to 'true' to keep current scenes loaded.")] string additive = "false")
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                return MCPToolResult.Error($"Scene not found at '{path}'.");

            var mode = additive == "true" ? OpenSceneMode.Additive : OpenSceneMode.Single;
            EditorSceneManager.OpenScene(path, mode);

            var loadedScenes = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                loadedScenes.Add(SceneManager.GetSceneAt(i).path);

            return MCPToolResult.Success(new { opened = path, mode = mode.ToString(), loadedScenes });
        }

        /// <summary>
        /// Duplicates a scene asset.
        /// </summary>
        /// <param name="sourcePath">Source scene path.</param>
        /// <param name="destPath">Destination path for the copy.</param>
        /// <returns>Both paths, or error if source not found.</returns>
        [MCPTool("scene_duplicate", "Duplicate a scene asset to a new path (does not open the copy)")]
        public static MCPToolResult DuplicateScene(
            [MCPToolParam("Source scene path", required: true)] string sourcePath,
            [MCPToolParam("Destination path for the copy", required: true)] string destPath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourcePath) == null)
                return MCPToolResult.Error($"Source scene not found at '{sourcePath}'.");

            var directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory))
                EnsureFolderExists(directory);

            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
                return MCPToolResult.Error($"Failed to copy scene to '{destPath}'.");

            return MCPToolResult.Success(new { source = sourcePath, created = destPath });
        }

        /// <summary>
        /// Lists all scenes in the Build Settings.
        /// </summary>
        /// <returns>Array of scenes with index, path, and enabled status.</returns>
        [MCPTool("scene_list_build", "List all scenes in Build Settings with index, path, and enabled status")]
        public static MCPToolResult ListBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Select((s, i) => new { index = i, path = s.path, enabled = s.enabled })
                .ToArray();

            return MCPToolResult.Success(new { scenes, count = scenes.Length });
        }

        /// <summary>
        /// Sets the Build Settings scene list from a JSON array.
        /// </summary>
        /// <param name="scenesJson">JSON array of scene entries with path and enabled fields.</param>
        /// <returns>The new scene list, or error if paths don't exist.</returns>
        [MCPTool("scene_set_build", "Set the Build Settings scene list. " +
            "JSON array of {\"path\": \"Assets/Scenes/X.unity\", \"enabled\": true}")]
        public static MCPToolResult SetBuildScenes(
            [MCPToolParam("JSON array of scene entries with path and enabled fields", required: true)] string scenesJson)
        {
            BuildSceneEntry[] entries;
            try
            {
                entries = JsonConvert.DeserializeObject<BuildSceneEntry[]>(scenesJson);
            }
            catch (Exception ex)
            {
                return MCPToolResult.Error($"Invalid JSON: {ex.Message}");
            }

            if (entries == null)
                return MCPToolResult.Error("scenesJson must be a non-null JSON array.");

            var missing = entries
                .Where(e => AssetDatabase.LoadAssetAtPath<SceneAsset>(e.Path) == null)
                .Select(e => e.Path)
                .ToArray();

            if (missing.Length > 0)
                return MCPToolResult.Error(
                    $"Scene(s) not found: {string.Join(", ", missing)}. " +
                    "All scenes must exist before adding to build settings.");

            var buildScenes = entries
                .Select(e => new EditorBuildSettingsScene(e.Path, e.Enabled))
                .ToArray();

            EditorBuildSettings.scenes = buildScenes;

            return MCPToolResult.Success(new
            {
                scenes = entries.Select((e, i) => new { index = i, e.Path, e.Enabled }),
                count = entries.Length
            });
        }

        // ── Helpers ──

        static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);
            var folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        // ── Data Models ──

        class BuildSceneEntry
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("enabled")]
            public bool Enabled { get; set; } = true;
        }
    }
}
