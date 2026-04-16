using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for inspecting and manipulating the Unity scene hierarchy.
    /// Provides tools for listing, creating, deleting, reparenting, and renaming GameObjects.
    /// </summary>
    public static class SceneTools
    {
        /// <summary>
        /// Lists all GameObjects in the active scene as a tree.
        /// </summary>
        /// <returns>A tree of all GameObjects with names, components, and active state.</returns>
        [MCPTool("scene_get_hierarchy", "List all GameObjects in the active scene as a tree of names, types, and active state")]
        public static MCPToolResult GetSceneHierarchy()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var tree = roots.Select(BuildNode).ToArray();
            return MCPToolResult.Success(tree);
        }

        /// <summary>
        /// Creates a new empty GameObject, optionally under a parent.
        /// Supports Undo so the operation can be reversed in the Editor.
        /// </summary>
        /// <param name="name">Name for the new GameObject.</param>
        /// <param name="parent">Parent GameObject path, or null/empty for root.</param>
        /// <returns>The path of the newly created GameObject.</returns>
        [MCPTool("scene_create_gameobject", "Create a new empty GameObject with optional parent (supports undo)")]
        public static MCPToolResult CreateGameObject(
            [MCPToolParam("Name for the new GameObject", required: true)] string name,
            [MCPToolParam("Parent GameObject path (omit for root)")] string parent)
        {
            var go = new GameObject(name);

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectResolver.FindByPath(parent);
                if (parentGo == null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    var rootNames = GetRootObjectNames();
                    return MCPToolResult.Error(
                        $"Parent not found: {parent}. Root objects in scene: {string.Join(", ", rootNames)}");
                }
                go.transform.SetParent(parentGo.transform);
            }

            Undo.RegisterCreatedObjectUndo(go, $"MCP Create {name}");
            return MCPToolResult.Success(new { path = GetPath(go) });
        }

        /// <summary>
        /// Deletes a GameObject from the scene with Undo support.
        /// </summary>
        /// <param name="path">Name or hierarchy path of the GameObject to delete.</param>
        /// <returns>Confirmation of the deleted path.</returns>
        [MCPTool("scene_delete_gameobject", "Delete a GameObject from the scene (supports undo)")]
        public static MCPToolResult DeleteGameObject(
            [MCPToolParam("GameObject name or path", required: true)] string path)
        {
            var go = GameObjectResolver.FindByPath(path);
            if (go == null)
            {
                var rootNames = GetRootObjectNames();
                return MCPToolResult.Error(
                    $"GameObject not found: {path}. Root objects in scene: {string.Join(", ", rootNames)}");
            }

            Undo.DestroyObjectImmediate(go);
            return MCPToolResult.Success(new { deleted = path });
        }

        /// <summary>
        /// Moves a GameObject under a new parent using Undo-safe operations.
        /// </summary>
        /// <param name="path">Name or hierarchy path of the GameObject to move.</param>
        /// <param name="newParent">Name or hierarchy path of the new parent, or empty string to move to root.</param>
        /// <returns>The new path of the reparented GameObject.</returns>
        [MCPTool("scene_reparent_gameobject", "Move a GameObject under a new parent (supports undo)")]
        public static MCPToolResult ReparentGameObject(
            [MCPToolParam("GameObject name or path to move", required: true)] string path,
            [MCPToolParam("New parent GameObject path (empty string for root)", required: true)] string newParent)
        {
            var go = GameObjectResolver.FindByPath(path);
            if (go == null)
            {
                var rootNames = GetRootObjectNames();
                return MCPToolResult.Error(
                    $"GameObject not found: {path}. Root objects in scene: {string.Join(", ", rootNames)}");
            }

            Transform newParentTransform = null;
            if (!string.IsNullOrEmpty(newParent))
            {
                var parentGo = GameObjectResolver.FindByPath(newParent);
                if (parentGo == null)
                {
                    var rootNames = GetRootObjectNames();
                    return MCPToolResult.Error(
                        $"GameObject not found: {newParent}. Root objects in scene: {string.Join(", ", rootNames)}");
                }
                newParentTransform = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, newParentTransform, $"MCP Reparent {go.name}");
            return MCPToolResult.Success(new { path = GetPath(go) });
        }

        /// <summary>
        /// Renames a GameObject using Undo-safe operations.
        /// </summary>
        /// <param name="path">Name or hierarchy path of the GameObject to rename.</param>
        /// <param name="newName">The new name for the GameObject.</param>
        /// <returns>The new path of the renamed GameObject.</returns>
        [MCPTool("scene_rename_gameobject", "Rename a GameObject (supports undo)")]
        public static MCPToolResult RenameGameObject(
            [MCPToolParam("GameObject name or path to rename", required: true)] string path,
            [MCPToolParam("New name for the GameObject", required: true)] string newName)
        {
            var go = GameObjectResolver.FindByPath(path);
            if (go == null)
            {
                var rootNames = GetRootObjectNames();
                return MCPToolResult.Error(
                    $"GameObject not found: {path}. Root objects in scene: {string.Join(", ", rootNames)}");
            }

            Undo.RecordObject(go, $"MCP Rename {go.name}");
            go.name = newName;
            return MCPToolResult.Success(new { path = GetPath(go) });
        }

        /// <summary>
        /// Creates multiple GameObjects with components, properties, and hierarchy in a single call.
        /// </summary>
        /// <param name="gameObjectsJson">JSON array of GameObject definitions.</param>
        /// <returns>Aggregated result with per-object status and errors.</returns>
        [MCPTool("scene_setup", "Create multiple GameObjects with components, properties, and hierarchy in a single batch call. " +
            "Accepts a JSON array where each entry defines a GameObject with optional position, rotation, scale, tag, layer, " +
            "components (with serialized properties), children (recursive), and parent (existing scene object name).")]
        public static MCPToolResult SceneSetup(
            [MCPToolParam("JSON array of GameObjects to create. Each object: { name (required), parent, tag, layer, " +
                "position: [x,y,z], rotation: [x,y,z], scale: [x,y,z], " +
                "components: [{ type, properties: { key: value } }], children: [recursive] }", required: true)] string gameObjectsJson)
        {
            GameObjectDef[] defs;
            try
            {
                defs = JsonConvert.DeserializeObject<GameObjectDef[]>(gameObjectsJson);
            }
            catch (Exception ex)
            {
                return MCPToolResult.Error($"Invalid JSON: {ex.Message}");
            }

            if (defs == null || defs.Length == 0)
                return MCPToolResult.Success(new { results = new object[0], errors = new object[0], summary = "0 GameObjects processed" });

            var results = new List<object>();
            var errors = new List<object>();

            Undo.IncrementCurrentGroup();

            foreach (var def in defs)
            {
                ProcessGameObjectDef(def, null, results, errors);
            }

            var goCount = results.Count;
            var errCount = errors.Count;
            Undo.SetCurrentGroupName($"Scene Setup: {goCount} GameObjects");

            return MCPToolResult.Success(new
            {
                results,
                errors,
                summary = $"{goCount} GameObjects created, {errCount} error(s)"
            });
        }

        /// <summary>
        /// Processes a single GameObjectDef: creates the GO, sets transform/tag/layer,
        /// adds components with properties, and recurses into children.
        /// </summary>
        static void ProcessGameObjectDef(GameObjectDef def, Transform parentTransform,
            List<object> results, List<object> errors)
        {
            if (string.IsNullOrEmpty(def.Name))
            {
                errors.Add(new { gameObject = "(unnamed)", operation = "create", error = "name is required" });
                return;
            }

            var go = new GameObject(def.Name);
            Undo.RegisterCreatedObjectUndo(go, $"MCP Create {def.Name}");

            // Parent: explicit parent field takes priority, then parentTransform from recursion
            if (!string.IsNullOrEmpty(def.Parent))
            {
                var parentGo = GameObjectResolver.FindByPath(def.Parent);
                if (parentGo != null)
                {
                    go.transform.SetParent(parentGo.transform);
                }
                else
                {
                    errors.Add(new { gameObject = def.Name, operation = "parent", error = $"Parent not found: {def.Parent}" });
                }
            }
            else if (parentTransform != null)
            {
                go.transform.SetParent(parentTransform);
            }

            // Transform
            if (def.Position != null && def.Position.Length == 3)
                go.transform.localPosition = new Vector3(def.Position[0], def.Position[1], def.Position[2]);

            if (def.Rotation != null && def.Rotation.Length == 3)
                go.transform.localEulerAngles = new Vector3(def.Rotation[0], def.Rotation[1], def.Rotation[2]);

            if (def.Scale != null && def.Scale.Length == 3)
                go.transform.localScale = new Vector3(def.Scale[0], def.Scale[1], def.Scale[2]);

            // Tag
            if (!string.IsNullOrEmpty(def.Tag))
            {
                try { go.tag = def.Tag; }
                catch (Exception ex) { errors.Add(new { gameObject = def.Name, operation = "tag", error = ex.Message }); }
            }

            // Layer
            if (!string.IsNullOrEmpty(def.Layer))
            {
                var layerIndex = LayerMask.NameToLayer(def.Layer);
                if (layerIndex >= 0)
                    go.layer = layerIndex;
                else
                    errors.Add(new { gameObject = def.Name, operation = "layer", error = $"Layer not found: {def.Layer}" });
            }

            // Components
            var addedComponents = new List<string>();
            if (def.Components != null)
            {
                foreach (var compDef in def.Components)
                {
                    if (string.IsNullOrEmpty(compDef.Type))
                    {
                        errors.Add(new { gameObject = def.Name, operation = "add_component", component = (string)null,
                            error = "Component type is required" });
                        continue;
                    }

                    var type = ComponentTools.FindComponentType(compDef.Type);
                    if (type == null)
                    {
                        errors.Add(new { gameObject = def.Name, operation = "add_component", component = compDef.Type,
                            error = $"Component type not found: '{compDef.Type}'" });
                        continue;
                    }

                    Undo.AddComponent(go, type);
                    addedComponents.Add(type.Name);

                    // Set properties on the just-added component
                    if (compDef.Properties != null && compDef.Properties.Count > 0)
                    {
                        var component = go.GetComponent(type);
                        if (component == null) continue;

                        var serializedObject = new SerializedObject(component);
                        foreach (var kvp in compDef.Properties)
                        {
                            var resolvedKey = ComponentTools.ResolvePropertyName(serializedObject, kvp.Key, out var resolveErr);
                            if (resolvedKey == null)
                            {
                                errors.Add(new { gameObject = def.Name, operation = "set_property",
                                    component = compDef.Type, property = kvp.Key,
                                    error = resolveErr });
                                continue;
                            }
                            var property = serializedObject.FindProperty(resolvedKey);

                            try
                            {
                                ComponentTools.SetSerializedPropertyValue(property, kvp.Value);
                            }
                            catch (Exception ex)
                            {
                                errors.Add(new { gameObject = def.Name, operation = "set_property",
                                    component = compDef.Type, property = kvp.Key, error = ex.Message });
                            }
                        }
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            results.Add(new { name = def.Name, success = true, components = addedComponents,
                parent = go.transform.parent != null ? go.transform.parent.name : (string)null });

            // Recurse into children
            if (def.Children != null)
            {
                foreach (var childDef in def.Children)
                {
                    ProcessGameObjectDef(childDef, go.transform, results, errors);
                }
            }
        }

        // ── Helpers ──

        /// <summary>
        /// Builds a hierarchical node representation of a GameObject and its children.
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
        /// Converts a Vector3 to an anonymous object with x, y, z properties.
        /// </summary>
        /// <param name="v">The Vector3 to convert.</param>
        /// <returns>An anonymous object with x, y, z fields.</returns>
        static object Vec3(Vector3 v) => new { x = v.x, y = v.y, z = v.z };

        /// <summary>
        /// Finds a Component type by name, searching all loaded assemblies.
        /// </summary>
        /// <param name="typeName">The simple or fully-qualified type name.</param>
        /// <returns>The matching Type, or null if not found.</returns>
        static Type FindComponentType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null && typeof(Component).IsAssignableFrom(type))
                        return type;

                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName && typeof(Component).IsAssignableFrom(t))
                            return t;
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException) { }
            }
            return null;
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

        // ── Data Models ──

        /// <summary>
        /// Defines a GameObject to create as part of a <see cref="SceneSetup"/> batch call.
        /// </summary>
        class GameObjectDef
        {
            /// <summary>Name for the new GameObject (required).</summary>
            [JsonProperty("name")] public string Name;
            /// <summary>Name or path of an existing scene object to parent under.</summary>
            [JsonProperty("parent")] public string Parent;
            /// <summary>Tag to assign (must already exist in the project's tag list).</summary>
            [JsonProperty("tag")] public string Tag;
            /// <summary>Layer name to assign.</summary>
            [JsonProperty("layer")] public string Layer;
            /// <summary>Local position as [x, y, z].</summary>
            [JsonProperty("position")] public float[] Position;
            /// <summary>Local Euler rotation as [x, y, z].</summary>
            [JsonProperty("rotation")] public float[] Rotation;
            /// <summary>Local scale as [x, y, z].</summary>
            [JsonProperty("scale")] public float[] Scale;
            /// <summary>Components to add, each with an optional property map.</summary>
            [JsonProperty("components")] public ComponentDef[] Components;
            /// <summary>Child GameObjects to create and nest under this one (recursive).</summary>
            [JsonProperty("children")] public GameObjectDef[] Children;
        }

        /// <summary>
        /// Defines a component to add to a GameObject, with optional serialized property overrides.
        /// </summary>
        class ComponentDef
        {
            /// <summary>Component type name (e.g. 'BoxCollider', 'Rigidbody').</summary>
            [JsonProperty("type")] public string Type;
            /// <summary>Serialized property names and string values to set after adding the component.</summary>
            [JsonProperty("properties")] public Dictionary<string, string> Properties;
        }
    }
}
