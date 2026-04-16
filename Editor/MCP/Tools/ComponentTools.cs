using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for inspecting and manipulating components on GameObjects.
    /// </summary>
    public static class ComponentTools
    {
        // ── Tools ──

        /// <summary>
        /// Adds a component to a GameObject by type name, with Undo support.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the target GameObject.</param>
        /// <param name="typeName">Component type name to add (e.g. 'BoxCollider', 'Rigidbody').</param>
        /// <returns>The added component type and target path, or a contextual error.</returns>
        [MCPTool("component_add", "Add a component to a GameObject by type name (supports undo)")]
        public static MCPToolResult AddComponent(
            [MCPToolParam("GameObject name or path (e.g. 'Canvas/Panel/Button')", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name (e.g. 'BoxCollider', 'Rigidbody')", required: true)] string typeName)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = FindComponentType(typeName);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{typeName}'. Ensure the type name is correct and the assembly containing it is loaded.");

            Undo.AddComponent(go, type);
            return MCPToolResult.Success(new { added = type.Name, to = GetPath(go) });
        }

        /// <summary>
        /// Finds all GameObjects in the active scene that have a specific component type.
        /// </summary>
        /// <param name="typeName">Component type name to search for (e.g. 'Camera', 'Rigidbody').</param>
        /// <returns>An array of matching GameObjects with names and paths, or an error if the type is not found.</returns>
        [MCPTool("component_find", "Find all GameObjects with a specific component type")]
        public static MCPToolResult FindComponents(
            [MCPToolParam("Component type name (e.g. 'Camera', 'UIDocument')", required: true)] string typeName)
        {
            var type = FindComponentType(typeName);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{typeName}'. Ensure the type name is correct and the assembly containing it is loaded.");

            var components = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
            var results = components
                .Select(c => c as Component)
                .Where(c => c != null)
                .Select(c => new { name = c.gameObject.name, path = GetPath(c.gameObject) })
                .ToArray();

            return MCPToolResult.Success(results);
        }

        /// <summary>
        /// Removes a component from a GameObject by type name, with Undo support.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the target GameObject.</param>
        /// <param name="typeName">Component type name to remove.</param>
        /// <returns>Confirmation of the removed component, or a contextual error listing existing component types.</returns>
        [MCPTool("component_remove", "Remove a component from a GameObject by type name (supports undo)")]
        public static MCPToolResult RemoveComponent(
            [MCPToolParam("GameObject name or path (e.g. 'Canvas/Panel/Button')", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name to remove (e.g. 'BoxCollider')", required: true)] string typeName)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = FindComponentType(typeName);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{typeName}'. Ensure the type name is correct and the assembly containing it is loaded.");

            var component = go.GetComponent(type);
            if (component == null)
            {
                var existing = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray();
                return MCPToolResult.Error(
                    $"Component '{typeName}' not found on GameObject '{GetPath(go)}'. " +
                    $"Existing components: {string.Join(", ", existing)}");
            }

            Undo.DestroyObjectImmediate(component);
            return MCPToolResult.Success(new { removed = type.Name, from = GetPath(go) });
        }

        /// <summary>
        /// Lists all components on a GameObject with their type names and enabled state.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the target GameObject.</param>
        /// <returns>An array of component info objects, or a contextual error if the GameObject is not found.</returns>
        [MCPTool("component_get_all", "List all components on a GameObject with type names and enabled state")]
        public static MCPToolResult GetComponents(
            [MCPToolParam("GameObject name or path (e.g. 'Canvas/Panel/Button')", required: true)] string gameObjectPath)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var components = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c =>
                {
                    var behaviour = c as Behaviour;
                    return new
                    {
                        type = c.GetType().Name,
                        enabled = behaviour != null ? (bool?)behaviour.enabled : null
                    };
                })
                .ToArray();

            return MCPToolResult.Success(new { gameObject = GetPath(go), components });
        }

        /// <summary>
        /// Reads a serialized property value from a component on a GameObject.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the target GameObject.</param>
        /// <param name="typeName">Component type name containing the property.</param>
        /// <param name="propertyName">Serialized property name to read.</param>
        /// <returns>The property value as a string representation, or a contextual error listing valid property names.</returns>
        [MCPTool("component_get_property", "Read a serialized property value from a component")]
        public static MCPToolResult GetComponentProperty(
            [MCPToolParam("GameObject name or path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name (e.g. 'Transform')", required: true)] string typeName,
            [MCPToolParam("Serialized property name (e.g. 'm_LocalPosition')", required: true)] string propertyName)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = FindComponentType(typeName);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{typeName}'.");

            var component = go.GetComponent(type);
            if (component == null)
            {
                var existing = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray();
                return MCPToolResult.Error(
                    $"Component '{typeName}' not found on '{GetPath(go)}'. " +
                    $"Existing components: {string.Join(", ", existing)}");
            }

            var serializedObject = new SerializedObject(component);
            var resolvedName = ResolvePropertyName(serializedObject, propertyName, out var resolveError);
            if (resolvedName == null)
                return MCPToolResult.Error(resolveError);
            var property = serializedObject.FindProperty(resolvedName);

            var value = GetSerializedPropertyValue(property);
            return MCPToolResult.Success(new
            {
                gameObject = GetPath(go),
                component = typeName,
                property = propertyName,
                propertyType = property.propertyType.ToString(),
                value
            });
        }

        /// <summary>
        /// Sets a serialized property value on a component using SerializedObject.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the target GameObject.</param>
        /// <param name="typeName">Component type name containing the property.</param>
        /// <param name="propertyName">Serialized property name to set.</param>
        /// <param name="value">The value to set, formatted as a string. For Vector3 use JSON: {"x":1,"y":2,"z":3}. For Color use JSON: {"r":1,"g":0,"b":0,"a":1}. For ObjectReference use an asset path.</param>
        /// <returns>Confirmation of the property change, or a contextual error listing valid properties with types.</returns>
        [MCPTool("component_set_property", "Set a serialized property value on a component")]
        public static MCPToolResult SetComponentProperty(
            [MCPToolParam("GameObject name or path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name (e.g. 'Transform')", required: true)] string typeName,
            [MCPToolParam("Serialized property name (e.g. 'm_LocalPosition')", required: true)] string propertyName,
            [MCPToolParam("Value to set (string, number, bool, or JSON for Vector3/Color)", required: true)] string value)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = FindComponentType(typeName);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{typeName}'.");

            var component = go.GetComponent(type);
            if (component == null)
            {
                var existing = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray();
                return MCPToolResult.Error(
                    $"Component '{typeName}' not found on '{GetPath(go)}'. " +
                    $"Existing components: {string.Join(", ", existing)}");
            }

            var serializedObject = new SerializedObject(component);
            var resolvedName = ResolvePropertyName(serializedObject, propertyName, out var resolveError);
            if (resolvedName == null)
                return MCPToolResult.Error(resolveError);
            var property = serializedObject.FindProperty(resolvedName);

            try
            {
                SetSerializedPropertyValue(property, value);
                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                return MCPToolResult.Error($"Failed to set property '{propertyName}': {ex.Message}");
            }

            return MCPToolResult.Success(new
            {
                gameObject = GetPath(go),
                component = typeName,
                property = propertyName,
                newValue = value
            });
        }

        /// <summary>
        /// Sets multiple serialized properties across multiple GameObjects and components in a single call.
        /// </summary>
        /// <param name="operationsJson">JSON array of property-setting operations.</param>
        /// <returns>Aggregated result with per-operation status and errors.</returns>
        [MCPTool("component_set_properties", "Set multiple serialized properties across multiple GameObjects and components in a single batch call. " +
            "Accepts a JSON array where each entry specifies a GameObject, component type, and a dictionary of property name-value pairs.")]
        public static MCPToolResult SetProperties(
            [MCPToolParam("JSON array of operations. Each: { gameObject (required), component (required), " +
                "properties: { propertyName: value } }", required: true)] string operationsJson)
        {
            PropertyOperationDef[] ops;
            try
            {
                ops = JsonConvert.DeserializeObject<PropertyOperationDef[]>(operationsJson);
            }
            catch (Exception ex)
            {
                return MCPToolResult.Error($"Invalid JSON: {ex.Message}");
            }

            if (ops == null || ops.Length == 0)
                return MCPToolResult.Success(new { results = new object[0], errors = new object[0], summary = "0 operations processed" });

            var results = new List<object>();
            var errors = new List<object>();

            Undo.IncrementCurrentGroup();

            foreach (var op in ops)
            {
                var go = FindGameObject(op.GameObject);
                if (go == null)
                {
                    errors.Add(new { gameObject = op.GameObject, error = $"GameObject not found: '{op.GameObject}'" });
                    continue;
                }

                var type = FindComponentType(op.Component);
                if (type == null)
                {
                    errors.Add(new { gameObject = op.GameObject, component = op.Component,
                        error = $"Component type not found: '{op.Component}'" });
                    continue;
                }

                var component = go.GetComponent(type);
                if (component == null)
                {
                    var existing = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToArray();
                    errors.Add(new { gameObject = op.GameObject, component = op.Component,
                        error = $"Component '{op.Component}' not found on '{GetPath(go)}'. Existing: {string.Join(", ", existing)}" });
                    continue;
                }

                var serializedObject = new SerializedObject(component);
                int propsSet = 0;
                var propErrors = new List<string>();

                if (op.Properties != null)
                {
                    foreach (var kvp in op.Properties)
                    {
                        var resolvedKey = ResolvePropertyName(serializedObject, kvp.Key, out var resolveErr);
                        if (resolvedKey == null)
                        {
                            propErrors.Add($"{kvp.Key}: {resolveErr}");
                            continue;
                        }
                        var property = serializedObject.FindProperty(resolvedKey);

                        try
                        {
                            // Normalize JToken to string for SetSerializedPropertyValue
                            string valueStr;
                            if (kvp.Value.Type == Newtonsoft.Json.Linq.JTokenType.String)
                                valueStr = kvp.Value.Value<string>();
                            else
                                valueStr = kvp.Value.ToString(Newtonsoft.Json.Formatting.None);

                            SetSerializedPropertyValue(property, valueStr);
                            propsSet++;
                        }
                        catch (Exception ex)
                        {
                            propErrors.Add($"{kvp.Key}: {ex.Message}");
                        }
                    }
                    if (propsSet > 0)
                        serializedObject.ApplyModifiedProperties();
                }

                var result = new Dictionary<string, object>
                {
                    { "gameObject", op.GameObject },
                    { "component", op.Component },
                    { "propertiesSet", propsSet }
                };
                if (propErrors.Count > 0)
                    result["errors"] = propErrors;

                results.Add(result);
            }

            var totalProps = 0;
            foreach (var r in results)
            {
                if (r is Dictionary<string, object> dict && dict.ContainsKey("propertiesSet"))
                    totalProps += (int)dict["propertiesSet"];
            }

            Undo.SetCurrentGroupName($"Set Properties: {totalProps} properties across {results.Count} operations");

            return MCPToolResult.Success(new
            {
                results,
                errors,
                summary = $"{totalProps} properties set across {results.Count} operation(s), {errors.Count} error(s)"
            });
        }

        /// <summary>
        /// Lists all serialized properties on a component with both serialized and display names.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the target GameObject.</param>
        /// <param name="typeName">Component type name to list properties for.</param>
        /// <returns>Array of property entries with serializedName, displayName, type, and currentValue.</returns>
        [MCPTool("component_list_properties", "List all serialized properties on a component with " +
            "both serialized names and display names, types, and current values")]
        public static MCPToolResult ListComponentProperties(
            [MCPToolParam("GameObject name or path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name (e.g. 'Camera', 'Transform')", required: true)] string typeName)
        {
            var go = FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = FindComponentType(typeName);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{typeName}'.");

            var component = go.GetComponent(type);
            if (component == null)
            {
                var existing = go.GetComponents<Component>()
                    .Where(c => c != null).Select(c => c.GetType().Name).ToArray();
                return MCPToolResult.Error(
                    $"Component '{typeName}' not found on '{GetPath(go)}'. " +
                    $"Existing components: {string.Join(", ", existing)}");
            }

            var serializedObject = new SerializedObject(component);
            var properties = new List<object>();
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    properties.Add(new
                    {
                        serializedName = iterator.name,
                        displayName = iterator.displayName,
                        type = iterator.propertyType.ToString(),
                        currentValue = GetSerializedPropertyValue(iterator)
                    });
                } while (iterator.NextVisible(false));
            }

            return MCPToolResult.Success(new
            {
                gameObject = GetPath(go),
                component = typeName,
                properties,
                count = properties.Count
            });
        }

        // ── Helpers ──

        /// <summary>
        /// Finds a GameObject by name or hierarchy path. Returns null if not found.
        /// </summary>
        /// <param name="path">Name or hierarchy path (e.g. 'Canvas/Panel/Button').</param>
        /// <returns>The found GameObject, or null.</returns>
        internal static GameObject FindGameObject(string path)
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
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects()
                .Select(go => go.name)
                .ToArray();
            return MCPToolResult.Error(
                $"GameObject not found: '{path}'. " +
                $"Root objects in scene: {string.Join(", ", roots)}");
        }

        /// <summary>
        /// Searches all loaded assemblies for a Component-derived type by name.
        /// Supports both fully-qualified and simple type names.
        /// </summary>
        /// <param name="typeName">The type name to search for.</param>
        /// <returns>The matching Type, or null if not found.</returns>
        internal static Type FindComponentType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Try fully-qualified name first
                    var type = assembly.GetType(typeName);
                    if (type != null && typeof(Component).IsAssignableFrom(type))
                        return type;

                    // Try simple name match
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
        /// Returns the full hierarchy path for a GameObject.
        /// </summary>
        /// <param name="go">The GameObject to get the path for.</param>
        /// <returns>The hierarchy path string (e.g. 'Parent/Child/GrandChild').</returns>
        internal static string GetPath(GameObject go)
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
        /// Reads a SerializedProperty value and returns a string representation
        /// based on its property type.
        /// </summary>
        /// <param name="prop">The serialized property to read.</param>
        /// <returns>A string representation of the property value.</returns>
        static string GetSerializedPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString();
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return $"{{\"r\":{c.r},\"g\":{c.g},\"b\":{c.b},\"a\":{c.a}}}";
                case SerializedPropertyType.ObjectReference:
                    var obj = prop.objectReferenceValue;
                    return obj != null ? obj.name : "null";
                case SerializedPropertyType.Enum:
                    return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                        ? prop.enumNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return $"{{\"x\":{v2.x},\"y\":{v2.y}}}";
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return $"{{\"x\":{v3.x},\"y\":{v3.y},\"z\":{v3.z}}}";
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return $"{{\"x\":{v4.x},\"y\":{v4.y},\"z\":{v4.z},\"w\":{v4.w}}}";
                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    return $"{{\"x\":{r.x},\"y\":{r.y},\"width\":{r.width},\"height\":{r.height}}}";
                case SerializedPropertyType.Bounds:
                    var b = prop.boundsValue;
                    return $"{{\"center\":{{\"x\":{b.center.x},\"y\":{b.center.y},\"z\":{b.center.z}}}," +
                           $"\"size\":{{\"x\":{b.size.x},\"y\":{b.size.y},\"z\":{b.size.z}}}}}";
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return $"{{\"x\":{q.x},\"y\":{q.y},\"z\":{q.z},\"w\":{q.w}}}";
                case SerializedPropertyType.Vector2Int:
                    var v2i = prop.vector2IntValue;
                    return $"{{\"x\":{v2i.x},\"y\":{v2i.y}}}";
                case SerializedPropertyType.Vector3Int:
                    var v3i = prop.vector3IntValue;
                    return $"{{\"x\":{v3i.x},\"y\":{v3i.y},\"z\":{v3i.z}}}";
                case SerializedPropertyType.RectInt:
                    var ri = prop.rectIntValue;
                    return $"{{\"x\":{ri.x},\"y\":{ri.y},\"width\":{ri.width},\"height\":{ri.height}}}";
                case SerializedPropertyType.BoundsInt:
                    var bi = prop.boundsIntValue;
                    return $"{{\"center\":{{\"x\":{bi.position.x},\"y\":{bi.position.y},\"z\":{bi.position.z}}}," +
                           $"\"size\":{{\"x\":{bi.size.x},\"y\":{bi.size.y},\"z\":{bi.size.z}}}}}";
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                default:
                    return $"<unsupported:{prop.propertyType}>";
            }
        }

        /// <summary>
        /// Sets a SerializedProperty value from a string representation,
        /// deserializing based on the property type.
        /// </summary>
        /// <param name="prop">The serialized property to set.</param>
        /// <param name="value">The string value to parse and assign.</param>
        /// <exception cref="ArgumentException">Thrown when the value cannot be parsed for the property type.</exception>
        internal static void SetSerializedPropertyValue(SerializedProperty prop, string value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = int.Parse(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = bool.Parse(value);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = float.Parse(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(value);
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ParseVector2(value);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ParseVector3(value);
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ParseVector4(value);
                    break;
                case SerializedPropertyType.Quaternion:
                    var qv = ParseVector4(value);
                    prop.quaternionValue = new Quaternion(qv.x, qv.y, qv.z, qv.w);
                    break;
                case SerializedPropertyType.Rect:
                    prop.rectValue = ParseRect(value);
                    break;
                case SerializedPropertyType.Vector2Int:
                    var v2 = ParseVector2(value);
                    prop.vector2IntValue = new Vector2Int(Mathf.RoundToInt(v2.x), Mathf.RoundToInt(v2.y));
                    break;
                case SerializedPropertyType.Vector3Int:
                    var v3 = ParseVector3(value);
                    prop.vector3IntValue = new Vector3Int(Mathf.RoundToInt(v3.x), Mathf.RoundToInt(v3.y), Mathf.RoundToInt(v3.z));
                    break;
                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out var enumIndex))
                    {
                        prop.enumValueIndex = enumIndex;
                    }
                    else
                    {
                        var idx = Array.IndexOf(prop.enumNames, value);
                        if (idx < 0)
                            throw new ArgumentException($"Invalid enum value '{value}'. Valid values: {string.Join(", ", prop.enumNames)}");
                        prop.enumValueIndex = idx;
                    }
                    break;
                case SerializedPropertyType.LayerMask:
                    prop.intValue = int.Parse(value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    if (string.IsNullOrEmpty(value) || value == "null")
                    {
                        prop.objectReferenceValue = null;
                    }
                    else
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                        if (asset == null)
                            throw new ArgumentException($"Asset not found at path: '{value}'");
                        prop.objectReferenceValue = asset;
                    }
                    break;
                default:
                    throw new ArgumentException($"Unsupported property type: {prop.propertyType}");
            }
        }

        /// <summary>
        /// Lists all visible serialized property names on a SerializedObject.
        /// </summary>
        /// <param name="serializedObject">The SerializedObject to enumerate.</param>
        /// <returns>An array of property name strings.</returns>
        static string[] ListSerializedPropertyNames(SerializedObject serializedObject)
        {
            var names = new System.Collections.Generic.List<string>();
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    names.Add(iterator.name);
                } while (iterator.NextVisible(false));
            }
            return names.ToArray();
        }

        /// <summary>
        /// Lists all visible serialized property names with their types on a SerializedObject.
        /// </summary>
        /// <param name="serializedObject">The SerializedObject to enumerate.</param>
        /// <returns>An array of strings in "name (Type)" format.</returns>
        static string[] ListSerializedPropertyNamesWithTypes(SerializedObject serializedObject)
        {
            var entries = new System.Collections.Generic.List<string>();
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    entries.Add($"{iterator.name} ({iterator.propertyType})");
                } while (iterator.NextVisible(false));
            }
            return entries.ToArray();
        }

        /// <summary>
        /// Resolves a property name input to a serialized property path.
        /// Tries exact match first, then display name match (case-insensitive),
        /// then normalized match (strips spaces/underscores/hyphens, lowercased).
        /// </summary>
        /// <param name="so">The SerializedObject to search.</param>
        /// <param name="input">The user-provided property name.</param>
        /// <param name="errorMessage">Set to an error string if resolution fails or is ambiguous.</param>
        /// <returns>The resolved serialized property path, or null if not found or ambiguous.</returns>
        internal static string ResolvePropertyName(SerializedObject so, string input, out string errorMessage)
        {
            errorMessage = null;

            // 1. Try exact match first (existing behavior)
            var prop = so.FindProperty(input);
            if (prop != null)
                return input;

            // 2. Build display name map
            var displayToPath = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            var normalizedToPath = new Dictionary<string, List<string>>();
            var iterator = so.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    var path = iterator.propertyPath;
                    var display = iterator.displayName;

                    // Display name (case-insensitive)
                    if (!displayToPath.ContainsKey(display))
                        displayToPath[display] = path;

                    // Normalized key
                    var normalized = NormalizePropertyName(display);
                    if (!normalizedToPath.ContainsKey(normalized))
                        normalizedToPath[normalized] = new List<string>();
                    normalizedToPath[normalized].Add(path);
                } while (iterator.NextVisible(false));
            }

            // 3. Try case-insensitive display name match
            if (displayToPath.TryGetValue(input, out var displayMatch))
                return displayMatch;

            // 4. Try normalized match
            var normalizedInput = NormalizePropertyName(input);
            if (normalizedToPath.TryGetValue(normalizedInput, out var candidates))
            {
                if (candidates.Count == 1)
                    return candidates[0];

                errorMessage = $"Ambiguous property name '{input}'. Matches: " +
                               string.Join(", ", candidates);
                return null;
            }

            // 5. Not found — list valid properties
            var validProps = ListSerializedPropertyNamesWithTypes(so);
            errorMessage = $"Property '{input}' not found on {so.targetObject.GetType().Name}. " +
                           $"Valid properties: {string.Join(", ", validProps)}";
            return null;
        }

        /// <summary>
        /// Normalizes a property name for fuzzy matching by stripping
        /// spaces, underscores, and hyphens, then lowercasing.
        /// </summary>
        /// <param name="name">The property name to normalize.</param>
        /// <returns>The normalized name string.</returns>
        static string NormalizePropertyName(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (ch != ' ' && ch != '_' && ch != '-')
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses a JSON string into a Vector3.
        /// Expected format: {"x":1,"y":2,"z":3}
        /// </summary>
        /// <param name="value">The JSON string to parse.</param>
        /// <returns>The parsed Vector3.</returns>
        static Vector3 ParseVector3(string value)
        {
            // Simple JSON parsing without dependency on JSON library
            var x = ExtractJsonFloat(value, "x");
            var y = ExtractJsonFloat(value, "y");
            var z = ExtractJsonFloat(value, "z");
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Parses a JSON string into a Vector2.
        /// Expected format: {"x":1,"y":2}
        /// </summary>
        /// <param name="value">The JSON string to parse.</param>
        /// <returns>The parsed Vector2.</returns>
        static Vector2 ParseVector2(string value)
        {
            var x = ExtractJsonFloat(value, "x");
            var y = ExtractJsonFloat(value, "y");
            return new Vector2(x, y);
        }

        /// <summary>
        /// Parses a JSON string into a Vector4.
        /// Expected format: {"x":1,"y":2,"z":3,"w":4}
        /// </summary>
        /// <param name="value">The JSON string to parse.</param>
        /// <returns>The parsed Vector4.</returns>
        static Vector4 ParseVector4(string value)
        {
            var x = ExtractJsonFloat(value, "x");
            var y = ExtractJsonFloat(value, "y");
            var z = ExtractJsonFloat(value, "z");
            var w = ExtractJsonFloat(value, "w");
            return new Vector4(x, y, z, w);
        }

        /// <summary>
        /// Parses a JSON string into a Color.
        /// Expected format: {"r":1,"g":0,"b":0,"a":1}
        /// </summary>
        /// <param name="value">The JSON string to parse.</param>
        /// <returns>The parsed Color.</returns>
        static Color ParseColor(string value)
        {
            var r = ExtractJsonFloat(value, "r");
            var g = ExtractJsonFloat(value, "g");
            var b = ExtractJsonFloat(value, "b");
            var a = ExtractJsonFloat(value, "a");
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Parses a JSON string into a Rect.
        /// Expected format: {"x":0,"y":0,"width":100,"height":50}
        /// </summary>
        /// <param name="value">The JSON string to parse.</param>
        /// <returns>The parsed Rect.</returns>
        static Rect ParseRect(string value)
        {
            var x = ExtractJsonFloat(value, "x");
            var y = ExtractJsonFloat(value, "y");
            var w = ExtractJsonFloat(value, "width");
            var h = ExtractJsonFloat(value, "height");
            return new Rect(x, y, w, h);
        }

        /// <summary>
        /// Extracts a float value from a simple JSON object by key name.
        /// Handles both integer and decimal number formats.
        /// </summary>
        /// <param name="json">The JSON string to search.</param>
        /// <param name="key">The key name to extract the value for.</param>
        /// <returns>The parsed float value.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is not found in the JSON string.</exception>
        static float ExtractJsonFloat(string json, string key)
        {
            // Find "key": or "key" :
            var keyPattern = $"\"{key}\"";
            var idx = json.IndexOf(keyPattern, StringComparison.Ordinal);
            if (idx < 0)
                throw new ArgumentException($"Key '{key}' not found in JSON: {json}");

            // Move past the key and colon
            idx += keyPattern.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':'))
                idx++;

            // Extract the number
            var start = idx;
            while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == '-' || json[idx] == 'e' || json[idx] == 'E' || json[idx] == '+'))
                idx++;

            var numberStr = json.Substring(start, idx - start);
            return float.Parse(numberStr, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ── Data Models ──

        /// <summary>
        /// Describes a batch property-setting operation for component_set_properties.
        /// </summary>
        class PropertyOperationDef
        {
            /// <summary>Name or path of the target GameObject.</summary>
            [JsonProperty("gameObject")] public string GameObject;
            /// <summary>Component type name (e.g. "Rigidbody", "BoxCollider").</summary>
            [JsonProperty("component")] public string Component;
            /// <summary>Serialized property name-value pairs to set.</summary>
            [JsonProperty("properties")] public Dictionary<string, Newtonsoft.Json.Linq.JToken> Properties;
        }
    }
}
