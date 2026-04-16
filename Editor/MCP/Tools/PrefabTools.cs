using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for creating, instantiating, and inspecting Unity prefabs.
    /// Provides tools for saving GameObjects as prefabs, instantiating prefabs into
    /// the scene, applying overrides, and inspecting prefab hierarchy contents.
    /// </summary>
    [InitializeOnLoad]
    public static class PrefabTools
    {
        /// <summary>
        /// Registers cleanup for prefab editing sessions before domain reload
        /// to prevent leaked LoadPrefabContents handles.
        /// </summary>
        static PrefabTools()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClosePrefabEditingSession;
        }
        /// <summary>
        /// Saves a scene GameObject as a prefab asset at the specified path.
        /// Creates parent directories if they do not exist.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the GameObject in the scene.</param>
        /// <param name="assetPath">Project-relative asset path for the prefab (e.g. "Assets/Prefabs/Player.prefab").</param>
        /// <returns>The created asset path, or a contextual error if the GameObject is not found or save fails.</returns>
        [MCPTool("prefab_create", "Save a scene GameObject as a prefab asset (creates parent directories if needed)")]
        public static MCPToolResult CreatePrefab(
            [MCPToolParam("Scene GameObject name or path (e.g. 'Player')", required: true)] string gameObjectPath,
            [MCPToolParam("Asset path for the prefab (e.g. 'Assets/Prefabs/Player.prefab')", required: true)] string assetPath)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            // Ensure parent directories exist
            var absolutePath = PathSandbox.ResolveWritable(assetPath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, assetPath, out success);
            if (!success || prefab == null)
                return MCPToolResult.Error($"Failed to save prefab at '{assetPath}'. Ensure the path ends with .prefab and is under the Assets folder.");

            return MCPToolResult.Success(new { createdAsset = assetPath });
        }

        /// <summary>
        /// Instantiates a prefab asset into the active scene, optionally under a parent GameObject.
        /// Registers the created object with Undo for editor undo support.
        /// </summary>
        /// <param name="prefabPath">Project-relative asset path of the prefab to instantiate.</param>
        /// <param name="parent">Optional parent GameObject name or path. Omit or pass empty for scene root.</param>
        /// <returns>The hierarchy path of the instantiated GameObject, or a contextual error.</returns>
        [MCPTool("prefab_instantiate", "Instantiate a prefab into the scene with optional parent (supports undo)")]
        public static MCPToolResult InstantiatePrefab(
            [MCPToolParam("Prefab asset path (e.g. 'Assets/Prefabs/Player.prefab')", required: true)] string prefabPath,
            [MCPToolParam("Parent GameObject path (omit for root)")] string parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return MCPToolResult.Error(
                    $"Prefab not found at '{prefabPath}'. Ensure the path is a valid project-relative asset path (e.g. 'Assets/Prefabs/MyPrefab.prefab').");

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return MCPToolResult.Error($"Failed to instantiate prefab at '{prefabPath}'.");

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectResolver.FindByPath(parent);
                if (parentGo == null)
                {
                    Object.DestroyImmediate(instance);
                    var rootNames = GetRootObjectNames();
                    return MCPToolResult.Error(
                        $"Parent not found: '{parent}'. Root objects in scene: {string.Join(", ", rootNames)}");
                }
                instance.transform.SetParent(parentGo.transform);
            }

            Undo.RegisterCreatedObjectUndo(instance, $"MCP Instantiate {prefab.name}");
            return MCPToolResult.Success(new { path = GetPath(instance) });
        }

        /// <summary>
        /// Applies all prefab overrides on a prefab instance back to the source prefab asset.
        /// The target GameObject must be a prefab instance in the scene.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the prefab instance in the scene.</param>
        /// <returns>Confirmation of the apply, or a contextual error if not found or not a prefab instance.</returns>
        [MCPTool("prefab_apply_overrides", "Apply all overrides on a prefab instance back to the source prefab asset")]
        public static MCPToolResult ApplyPrefabOverrides(
            [MCPToolParam("Prefab instance GameObject name or path", required: true)] string gameObjectPath)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return MCPToolResult.Error(
                    $"GameObject '{GetPath(go)}' is not a prefab instance. " +
                    "Only prefab instances in the scene can have overrides applied.");

            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return MCPToolResult.Success(new { applied = GetPath(go), sourcePrefab = sourcePath });
        }

        /// <summary>
        /// Loads and inspects the contents of a prefab asset without instantiating it in the scene.
        /// Returns a hierarchy tree of GameObjects with their names, components, and children.
        /// </summary>
        /// <param name="prefabPath">Project-relative asset path of the prefab to inspect.</param>
        /// <returns>A hierarchy tree of the prefab contents, or an error if not found.</returns>
        [MCPTool("prefab_get_contents", "Inspect a prefab asset's hierarchy without instantiating it in the scene")]
        public static MCPToolResult GetPrefabContents(
            [MCPToolParam("Prefab asset path (e.g. 'Assets/Prefabs/Player.prefab')", required: true)] string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return MCPToolResult.Error(
                    $"Prefab not found at '{prefabPath}'. Ensure the path is a valid project-relative asset path (e.g. 'Assets/Prefabs/MyPrefab.prefab').");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var tree = BuildNode(root);
                return MCPToolResult.Success(new { prefab = prefabPath, hierarchy = tree });
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── Prefab Editing Session State ──

        static GameObject _editingRoot;
        static string _editingPrefabPath;

        /// <summary>
        /// Sets a single property inside a prefab asset without instantiating it in the scene.
        /// Handles the LoadPrefabContents/Save/Unload cycle internally.
        /// </summary>
        /// <param name="prefabPath">Prefab asset path.</param>
        /// <param name="componentType">Component type name.</param>
        /// <param name="propertyName">Property name to set.</param>
        /// <param name="value">Value to set.</param>
        /// <returns>Confirmation of the edit, or a contextual error.</returns>
        [MCPTool("prefab_edit_property", "Set a serialized property inside a prefab asset (atomic load/edit/save)")]
        public static MCPToolResult EditPrefabProperty(
            [MCPToolParam("Prefab asset path", required: true)] string prefabPath,
            [MCPToolParam("Component type name", required: true)] string componentType,
            [MCPToolParam("Property name", required: true)] string propertyName,
            [MCPToolParam("Value to set", required: true)] string value)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return MCPToolResult.Error($"Prefab not found at '{prefabPath}'.");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var type = ComponentTools.FindComponentType(componentType);
                if (type == null)
                    return MCPToolResult.Error($"Component type not found: '{componentType}'.");

                var component = root.GetComponent(type);
                if (component == null)
                {
                    var existing = root.GetComponents<Component>()
                        .Where(c => c != null).Select(c => c.GetType().Name).ToArray();
                    return MCPToolResult.Error(
                        $"Component '{componentType}' not found on prefab root. " +
                        $"Existing: {string.Join(", ", existing)}");
                }

                var so = new SerializedObject(component);
                var prop = so.FindProperty(propertyName);
                if (prop == null)
                    return MCPToolResult.Error($"Property '{propertyName}' not found on {componentType}.");

                ComponentTools.SetSerializedPropertyValue(prop, value);
                so.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return MCPToolResult.Success(new
            {
                prefab = prefabPath,
                component = componentType,
                property = propertyName,
                newValue = value
            });
        }

        /// <summary>
        /// Opens a prefab for multi-edit session. Returns the root path so other tools
        /// (component, reference, event) can target the prefab contents.
        /// </summary>
        /// <param name="prefabPath">Prefab asset path.</param>
        /// <returns>Root path and components, or error if another prefab is open.</returns>
        [MCPTool("prefab_open_editing", "Open a prefab for multi-edit session. " +
            "Use existing component/reference/event tools on the returned root path, then call prefab_save_editing.")]
        public static MCPToolResult OpenPrefabEditing(
            [MCPToolParam("Prefab asset path", required: true)] string prefabPath)
        {
            if (_editingRoot != null)
                return MCPToolResult.Error(
                    $"A prefab is already open for editing: '{_editingPrefabPath}'. " +
                    "Call prefab_save_editing first.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return MCPToolResult.Error($"Prefab not found at '{prefabPath}'.");

            _editingRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            _editingPrefabPath = prefabPath;

            return MCPToolResult.Success(new
            {
                prefab = prefabPath,
                rootPath = _editingRoot.name,
                components = _editingRoot.GetComponents<Component>()
                    .Where(c => c != null).Select(c => c.GetType().Name).ToArray()
            });
        }

        /// <summary>
        /// Saves and closes the currently open prefab editing session.
        /// </summary>
        /// <returns>Confirmation with saved path, or error if no session open.</returns>
        [MCPTool("prefab_save_editing", "Save and close the current prefab editing session")]
        public static MCPToolResult SavePrefabEditing()
        {
            if (_editingRoot == null)
                return MCPToolResult.Error("No prefab is currently open for editing. Call prefab_open_editing first.");

            var path = _editingPrefabPath;
            PrefabUtility.SaveAsPrefabAsset(_editingRoot, _editingPrefabPath);
            PrefabUtility.UnloadPrefabContents(_editingRoot);
            _editingRoot = null;
            _editingPrefabPath = null;

            return MCPToolResult.Success(new { saved = path });
        }

        /// <summary>
        /// Closes the editing session without saving. Internal use for cleanup.
        /// </summary>
        internal static void ClosePrefabEditingSession()
        {
            if (_editingRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(_editingRoot);
                _editingRoot = null;
                _editingPrefabPath = null;
            }
        }

        /// <summary>
        /// Creates a prefab variant from a base prefab.
        /// </summary>
        /// <param name="basePrefabPath">Base prefab asset path.</param>
        /// <param name="variantPath">Variant asset path.</param>
        /// <returns>Both paths, or error if base not found.</returns>
        [MCPTool("prefab_create_variant", "Create a prefab variant from a base prefab")]
        public static MCPToolResult CreatePrefabVariant(
            [MCPToolParam("Base prefab asset path", required: true)] string basePrefabPath,
            [MCPToolParam("Variant asset path", required: true)] string variantPath)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            if (basePrefab == null)
                return MCPToolResult.Error($"Base prefab not found at '{basePrefabPath}'.");

            var instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (instance == null)
                return MCPToolResult.Error($"Failed to instantiate base prefab.");

            var directory = System.IO.Path.GetDirectoryName(variantPath);
            if (!string.IsNullOrEmpty(directory))
                EnsureFolderExists(directory);

            bool success;
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, variantPath, InteractionMode.AutomatedAction, out success);
            Object.DestroyImmediate(instance);

            if (!success)
                return MCPToolResult.Error($"Failed to save variant at '{variantPath}'.");

            return MCPToolResult.Success(new { basePrefab = basePrefabPath, variant = variantPath });
        }

        // ── Helpers ──

        /// <summary>
        /// Finds a GameObject in the active scene by name or hierarchy path.
        /// </summary>
        /// <param name="path">Name or hierarchy path (e.g. "Canvas/Panel/Button").</param>
        /// <returns>The found GameObject, or null if not found.</returns>
        static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);
            var folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        static GameObject FindGameObject(string path)
        {
            return GameObjectResolver.FindByPath(path);
        }

        /// <summary>
        /// Creates a contextual error for when a GameObject cannot be found,
        /// listing the root objects in the active scene as suggestions.
        /// </summary>
        /// <param name="path">The path that was searched for.</param>
        /// <returns>An error MCPToolResult with root object suggestions.</returns>
        static MCPToolResult GameObjectNotFoundError(string path)
        {
            var rootNames = GetRootObjectNames();
            return MCPToolResult.Error(
                $"GameObject not found: '{path}'. " +
                $"Root objects in scene: {string.Join(", ", rootNames)}");
        }

        /// <summary>
        /// Gets the names of all root GameObjects in the active scene, for contextual error messages.
        /// </summary>
        /// <returns>An array of root GameObject names.</returns>
        static string[] GetRootObjectNames()
        {
            return SceneManager.GetActiveScene()
                .GetRootGameObjects()
                .Select(r => r.name)
                .ToArray();
        }

        /// <summary>
        /// Gets the full hierarchy path of a GameObject.
        /// </summary>
        /// <param name="go">The GameObject to get the path for.</param>
        /// <returns>The full hierarchy path (e.g. "Canvas/Panel/Button").</returns>
        static string GetPath(GameObject go)
        {
            var path = go.name;
            var current = go.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        /// <summary>
        /// Builds a hierarchical node representation of a GameObject and its children,
        /// including name, active state, component types, and child nodes.
        /// </summary>
        /// <param name="go">The GameObject to build a node for.</param>
        /// <returns>An anonymous object representing the node tree.</returns>
        static object BuildNode(GameObject go)
        {
            return new
            {
                name = go.name,
                active = go.activeSelf,
                components = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                children = Enumerable.Range(0, go.transform.childCount)
                    .Select(i => BuildNode(go.transform.GetChild(i).gameObject))
                    .ToArray()
            };
        }
    }
}
