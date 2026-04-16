using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for inspecting and selecting GameObjects in the Unity Editor.
    /// SelectGameObject sets the Editor selection; InspectGameObject dumps all
    /// component properties via SerializedObject for full inspection.
    /// </summary>
    public static class InspectorTools
    {
        /// <summary>
        /// Selects a GameObject in the Unity Editor, making it the active selection
        /// in the Hierarchy and Inspector windows.
        /// </summary>
        /// <param name="path">Name or hierarchy path of the GameObject to select.</param>
        /// <returns>The path of the selected GameObject, or an error if not found.</returns>
        [MCPTool("inspector_select", "Select a GameObject in the Editor (highlights in Hierarchy and Inspector)")]
        public static MCPToolResult SelectGameObject(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string path)
        {
            var go = GameObjectResolver.FindByPath(path);
            if (go == null)
            {
                var rootNames = GetRootObjectNames();
                return MCPToolResult.Error(
                    $"GameObject not found: '{path}'. Root objects in scene: {string.Join(", ", rootNames)}");
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            return MCPToolResult.Success(new
            {
                selected = GetPath(go),
                instanceId = go.GetInstanceID()
            });
        }

        /// <summary>
        /// Inspects a GameObject by dumping all component properties via SerializedObject.
        /// Returns transform data, component types, and every serialized property with
        /// name, type, and value for comprehensive inspection.
        /// </summary>
        /// <param name="path">Name or hierarchy path of the GameObject to inspect.</param>
        /// <returns>Full property dump of all components, or an error if not found.</returns>
        [MCPTool("inspector_inspect", "Get full property dump of a GameObject — all components and their serialized properties")]
        public static MCPToolResult InspectGameObject(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string path)
        {
            var go = GameObjectResolver.FindByPath(path);
            if (go == null)
            {
                var rootNames = GetRootObjectNames();
                return MCPToolResult.Error(
                    $"GameObject not found: '{path}'. Root objects in scene: {string.Join(", ", rootNames)}");
            }

            var components = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => DumpComponent(c))
                .ToArray();

            return MCPToolResult.Success(new
            {
                name = go.name,
                path = GetPath(go),
                active = go.activeSelf,
                layer = LayerMask.LayerToName(go.layer),
                tag = go.tag,
                isStatic = go.isStatic,
                transform = new
                {
                    position = Vec3(go.transform.position),
                    rotation = Vec3(go.transform.eulerAngles),
                    scale = Vec3(go.transform.localScale)
                },
                childCount = go.transform.childCount,
                children = Enumerable.Range(0, go.transform.childCount)
                    .Select(i => go.transform.GetChild(i).name)
                    .ToArray(),
                components
            });
        }

        /// <summary>
        /// Dumps all serialized properties of a component using SerializedObject.
        /// </summary>
        /// <param name="component">The component to dump.</param>
        /// <returns>An anonymous object with type name and property list.</returns>
        static object DumpComponent(Component component)
        {
            var so = new SerializedObject(component);
            var properties = new List<object>();

            var iterator = so.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    properties.Add(new
                    {
                        name = iterator.name,
                        displayName = iterator.displayName,
                        type = iterator.propertyType.ToString(),
                        value = GetPropertyValue(iterator)
                    });
                } while (iterator.NextVisible(false));
            }

            return new
            {
                type = component.GetType().Name,
                fullType = component.GetType().FullName,
                enabled = IsComponentEnabled(component),
                properties
            };
        }

        /// <summary>
        /// Extracts the value of a SerializedProperty as a string representation.
        /// </summary>
        /// <param name="prop">The serialized property to read.</param>
        /// <returns>A string representation of the property value.</returns>
        static string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("G");
                case SerializedPropertyType.String:
                    return prop.stringValue ?? "";
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})"
                        : "(null)";
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 &&
                           prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                default:
                    return $"({prop.propertyType})";
            }
        }

        /// <summary>
        /// Checks if a component is enabled (for Behaviours) or returns true for non-behaviour components.
        /// </summary>
        /// <param name="component">The component to check.</param>
        /// <returns>True if enabled or not a behaviour, false if disabled.</returns>
        static bool IsComponentEnabled(Component component)
        {
            if (component is Behaviour behaviour)
                return behaviour.enabled;
            if (component is Renderer renderer)
                return renderer.enabled;
            if (component is Collider collider)
                return collider.enabled;
            return true;
        }

        // ── Helpers ──

        /// <summary>
        /// Gets the full hierarchy path of a GameObject.
        /// </summary>
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
        static object Vec3(Vector3 v) => new { x = v.x, y = v.y, z = v.z };

        /// <summary>
        /// Gets root GameObject names from the active scene for contextual error messages.
        /// </summary>
        static string[] GetRootObjectNames()
        {
            return SceneManager.GetActiveScene()
                .GetRootGameObjects()
                .Select(r => r.name)
                .ToArray();
        }
    }
}
