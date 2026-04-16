using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for wiring object references between scene GameObjects and assets.
    /// Supports both scene hierarchy paths and asset paths as reference targets.
    /// </summary>
    public static class ReferenceTools
    {
        /// <summary>
        /// Sets an object reference field on a component. Accepts either a scene object path
        /// or an asset path as the target.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the source GameObject.</param>
        /// <param name="componentType">Component type containing the field.</param>
        /// <param name="propertyName">Serialized property name of the ObjectReference field.</param>
        /// <param name="targetPath">Scene hierarchy path of the target object.</param>
        /// <param name="targetAssetPath">Asset path of the target.</param>
        /// <param name="targetComponentType">Component type on the target to reference.</param>
        /// <returns>Confirmation of the assignment, or a contextual error.</returns>
        [MCPTool("reference_set", "Set an object reference field on a component. " +
            "Provide either targetPath (scene hierarchy path) or targetAssetPath (asset path), not both. " +
            "Use targetComponentType to reference a specific component on the target GameObject.")]
        public static MCPToolResult SetReference(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name (e.g. 'GameManager')", required: true)] string componentType,
            [MCPToolParam("Serialized property name (e.g. '_scoreText', 'm_ConnectedBody')", required: true)] string propertyName,
            [MCPToolParam("Target scene GameObject path (e.g. 'Canvas/ScoreText')")] string targetPath = null,
            [MCPToolParam("Target asset path (e.g. 'Assets/Sprites/hero.png')")] string targetAssetPath = null,
            [MCPToolParam("Component type on target to reference (e.g. 'Rigidbody', 'Text'). " +
                "Omit to reference the GameObject itself.")] string targetComponentType = null)
        {
            bool hasTargetPath = !string.IsNullOrEmpty(targetPath);
            bool hasAssetPath = !string.IsNullOrEmpty(targetAssetPath);
            if (!hasTargetPath && !hasAssetPath)
                return MCPToolResult.Error(
                    "Must provide either targetPath (scene object) or targetAssetPath (asset). Neither was provided.");
            if (hasTargetPath && hasAssetPath)
                return MCPToolResult.Error(
                    "Provide either targetPath or targetAssetPath, not both.");

            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = ComponentTools.FindComponentType(componentType);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{componentType}'.");

            var component = go.GetComponent(type);
            if (component == null)
            {
                var existing = go.GetComponents<Component>()
                    .Where(c => c != null).Select(c => c.GetType().Name).ToArray();
                return MCPToolResult.Error(
                    $"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'. " +
                    $"Existing components: {string.Join(", ", existing)}");
            }

            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                // Try to grow array if this is an array element path like "myArray.Array.data[0]"
                prop = ResolveOrGrowArrayElement(so, propertyName);
            }
            if (prop == null)
            {
                var validProps = ListObjectReferenceProperties(so);
                return MCPToolResult.Error(
                    $"Property '{propertyName}' not found on {componentType}. " +
                    $"ObjectReference properties: {string.Join(", ", validProps)}");
            }
            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                return MCPToolResult.Error(
                    $"Property '{propertyName}' is type {prop.propertyType}, not ObjectReference.");

            UnityEngine.Object targetObj;
            string targetDescription;

            if (hasTargetPath)
            {
                var targetGO = GameObjectResolver.FindByPath(targetPath);
                if (targetGO == null)
                    return GameObjectNotFoundError(targetPath);

                if (!string.IsNullOrEmpty(targetComponentType))
                {
                    var targetType = ComponentTools.FindComponentType(targetComponentType);
                    if (targetType == null)
                        return MCPToolResult.Error($"Target component type not found: '{targetComponentType}'.");

                    var targetComp = targetGO.GetComponent(targetType);
                    if (targetComp == null)
                    {
                        var availableComps = targetGO.GetComponents<Component>()
                            .Where(c => c != null).Select(c => c.GetType().Name).ToArray();
                        return MCPToolResult.Error(
                            $"Component '{targetComponentType}' not found on '{targetPath}'. " +
                            $"Available: {string.Join(", ", availableComps)}");
                    }
                    targetObj = targetComp;
                    targetDescription = $"{targetPath} ({targetComponentType})";
                }
                else
                {
                    targetObj = targetGO;
                    targetDescription = targetPath;
                }
            }
            else
            {
                var fieldType = GetObjectReferenceFieldType(prop);
                var resolveErr = ResolveAsset(targetAssetPath, fieldType, out targetObj);
                if (resolveErr != null)
                    return MCPToolResult.Error(resolveErr);
                targetDescription = targetAssetPath;
            }

            if (hasTargetPath)
            {
                var fieldType2 = GetObjectReferenceFieldType(prop);
                if (fieldType2 != null && !fieldType2.IsInstanceOfType(targetObj))
                    return MCPToolResult.Error(
                        $"Type mismatch: field '{propertyName}' expects {fieldType2.Name}, " +
                        $"but target is {targetObj.GetType().Name}. " +
                        (string.IsNullOrEmpty(targetComponentType)
                            ? $"Try specifying targetComponentType to reference a component instead of the GameObject."
                            : ""));
            }

            Undo.RecordObject(component, $"MCP Set Reference {propertyName}");
            prop.objectReferenceValue = targetObj;
            so.ApplyModifiedProperties();

            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                component = componentType,
                property = propertyName,
                target = targetDescription,
                targetType = targetObj.GetType().Name
            });
        }

        /// <summary>
        /// Reads the current value of an object reference field.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the GameObject.</param>
        /// <param name="componentType">Component type containing the field.</param>
        /// <param name="propertyName">Serialized property name.</param>
        /// <returns>Reference value info including name, type, and null status.</returns>
        [MCPTool("reference_get", "Get the current value of an object reference field on a component")]
        public static MCPToolResult GetReference(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type name", required: true)] string componentType,
            [MCPToolParam("Serialized property name", required: true)] string propertyName)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = ComponentTools.FindComponentType(componentType);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{componentType}'.");

            var component = go.GetComponent(type);
            if (component == null)
                return MCPToolResult.Error($"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'.");

            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
                return MCPToolResult.Error($"Property '{propertyName}' not found on {componentType}.");
            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                return MCPToolResult.Error($"Property '{propertyName}' is type {prop.propertyType}, not ObjectReference.");

            var obj = prop.objectReferenceValue;
            if (obj == null)
            {
                return MCPToolResult.Success(new
                {
                    gameObject = ComponentTools.GetPath(go),
                    component = componentType,
                    property = propertyName,
                    value = (string)null,
                    isNull = true
                });
            }

            var assetPath = AssetDatabase.GetAssetPath(obj);
            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                component = componentType,
                property = propertyName,
                value = obj.name,
                type = obj.GetType().Name,
                assetPath = string.IsNullOrEmpty(assetPath) ? null : assetPath,
                isNull = false
            });
        }

        /// <summary>
        /// Scans a component (or all components) for unset ObjectReference fields.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the GameObject.</param>
        /// <param name="componentType">Optional component type to scan. Omit to scan all.</param>
        /// <returns>List of unset reference fields with expected types.</returns>
        [MCPTool("reference_find_unset", "Find all unset (null) object reference fields on a GameObject's components")]
        public static MCPToolResult FindUnsetReferences(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type to scan (omit to scan all components)")] string componentType = null)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var components = new List<Component>();
            if (!string.IsNullOrEmpty(componentType))
            {
                var type = ComponentTools.FindComponentType(componentType);
                if (type == null)
                    return MCPToolResult.Error($"Component type not found: '{componentType}'.");
                var comp = go.GetComponent(type);
                if (comp == null)
                    return MCPToolResult.Error($"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'.");
                components.Add(comp);
            }
            else
            {
                components.AddRange(go.GetComponents<Component>().Where(c => c != null));
            }

            var unset = new List<object>();
            foreach (var comp in components)
            {
                var so = new SerializedObject(comp);
                var iterator = so.GetIterator();
                if (iterator.Next(true))
                {
                    do
                    {
                        if (iterator.depth > 1) continue;
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference
                            && iterator.objectReferenceValue == null
                            && iterator.name != "m_Script")
                        {
                            var fieldType = GetObjectReferenceFieldType(iterator);
                            unset.Add(new
                            {
                                component = comp.GetType().Name,
                                property = iterator.name,
                                expectedType = fieldType?.Name ?? "Object"
                            });
                        }
                    } while (iterator.Next(false));
                }
            }

            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                unsetReferences = unset,
                count = unset.Count
            });
        }

        // ── Helpers ──

        /// <summary>
        /// Creates a contextual error for when a GameObject cannot be found,
        /// listing the root objects in the active scene as suggestions.
        /// </summary>
        /// <param name="path">The path that was searched for.</param>
        /// <returns>An error MCPToolResult with root object suggestions.</returns>
        static MCPToolResult GameObjectNotFoundError(string path)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(r => r.name).ToArray();
            return MCPToolResult.Error(
                $"GameObject not found: '{path}'. Root objects in scene: {string.Join(", ", roots)}");
        }

        /// <summary>
        /// Lists the names of all ObjectReference-type properties on a SerializedObject.
        /// </summary>
        /// <param name="so">The SerializedObject to enumerate.</param>
        /// <returns>An array of property name strings for ObjectReference fields.</returns>
        static string[] ListObjectReferenceProperties(SerializedObject so)
        {
            var props = new List<string>();
            var iterator = so.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        props.Add(iterator.name);
                } while (iterator.NextVisible(false));
            }
            return props.ToArray();
        }

        /// <summary>
        /// If the property path is an array element (contains "Array.data[N]") and the element
        /// doesn't exist yet, grows the array to accommodate it, then returns the property.
        /// </summary>
        /// <param name="so">The SerializedObject to search.</param>
        /// <param name="propertyName">The full property path including array index.</param>
        /// <returns>The resolved SerializedProperty, or null if the path isn't an array element or the base array doesn't exist.</returns>
        static SerializedProperty ResolveOrGrowArrayElement(SerializedObject so, string propertyName)
        {
            // Match pattern: "fieldName.Array.data[N]"
            var arrayDataIdx = propertyName.IndexOf(".Array.data[", StringComparison.Ordinal);
            if (arrayDataIdx < 0) return null;

            var arrayFieldName = propertyName.Substring(0, arrayDataIdx);
            var bracketStart = propertyName.IndexOf('[', arrayDataIdx);
            var bracketEnd = propertyName.IndexOf(']', bracketStart);
            if (bracketStart < 0 || bracketEnd < 0) return null;

            var indexStr = propertyName.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            if (!int.TryParse(indexStr, out var targetIndex)) return null;

            var arrayProp = so.FindProperty(arrayFieldName);
            if (arrayProp == null || !arrayProp.isArray) return null;

            // Grow array to accommodate the target index
            while (arrayProp.arraySize <= targetIndex)
            {
                arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            }
            so.ApplyModifiedProperties();

            // Re-fetch after growth
            return so.FindProperty(propertyName);
        }

        /// <summary>
        /// Resolves an asset path to a sub-asset if the main asset type doesn't match
        /// the target field type. Supports explicit :: syntax and auto-resolution.
        /// </summary>
        /// <param name="assetPath">Asset path, optionally with ::SubAssetName suffix.</param>
        /// <param name="targetType">The expected type from the target field.</param>
        /// <param name="mainAsset">Output: the resolved asset (main or sub).</param>
        /// <returns>Null on success, or an error message string.</returns>
        static string ResolveAsset(string assetPath, Type targetType, out UnityEngine.Object mainAsset)
        {
            mainAsset = null;

            // Check for explicit sub-asset syntax: "Assets/Sprites/Sheet.png::SpriteName"
            var delimIdx = assetPath.IndexOf("::", StringComparison.Ordinal);
            string actualPath = delimIdx >= 0 ? assetPath.Substring(0, delimIdx) : assetPath;
            string subAssetName = delimIdx >= 0 ? assetPath.Substring(delimIdx + 2) : null;

            var loaded = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(actualPath);
            if (loaded == null)
                return $"Asset not found at path: '{actualPath}'.";

            // Explicit sub-asset name requested
            if (subAssetName != null)
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(actualPath);
                foreach (var a in allAssets)
                {
                    if (a != null && a.name == subAssetName)
                    {
                        mainAsset = a;
                        return null;
                    }
                }
                var names = allAssets.Where(a => a != null && a != loaded)
                    .Select(a => $"{a.name} ({a.GetType().Name})").ToArray();
                return $"Sub-asset '{subAssetName}' not found in '{actualPath}'. " +
                       $"Available sub-assets: {(names.Length > 0 ? string.Join(", ", names) : "none")}";
            }

            // Main asset matches target type — use it directly
            if (targetType == null || targetType.IsInstanceOfType(loaded))
            {
                mainAsset = loaded;
                return null;
            }

            // Auto-resolve: find a sub-asset matching the target type
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(actualPath)
                .Where(a => a != null && targetType.IsInstanceOfType(a))
                .ToArray();

            if (subAssets.Length == 1)
            {
                mainAsset = subAssets[0];
                return null;
            }

            if (subAssets.Length > 1)
            {
                var subNames = subAssets.Select(a => a.name).ToArray();
                return $"Multiple {targetType.Name} sub-assets found in '{actualPath}': " +
                       $"{string.Join(", ", subNames)}. Use '::SubAssetName' syntax to specify which one " +
                       $"(e.g. '{actualPath}::{subNames[0]}').";
            }

            // No matching sub-asset and main asset doesn't match target type
            return $"Type mismatch: field expects {targetType.Name}, but '{actualPath}' is {loaded.GetType().Name} " +
                   $"and contains no {targetType.Name} sub-assets.";
        }

        /// <summary>
        /// Attempts to determine the expected type of an ObjectReference field
        /// using reflection on the component's C# type.
        /// </summary>
        /// <param name="prop">The serialized property to inspect.</param>
        /// <returns>The field's declared type, or null if it cannot be determined.</returns>
        static Type GetObjectReferenceFieldType(SerializedProperty prop)
        {
            var targetObject = prop.serializedObject.targetObject;
            if (targetObject == null) return null;

            var objType = targetObject.GetType();
            var fieldInfo = objType.GetField(prop.name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (fieldInfo != null) return fieldInfo.FieldType;

            // Fallback for built-in Unity types: map m_FieldName → fieldName property
            var propName = prop.name;
            if (propName.StartsWith("m_") && propName.Length > 2)
            {
                var csName = char.ToLower(propName[2]) + propName.Substring(3);
                var propInfo = objType.GetProperty(csName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public);
                if (propInfo != null) return propInfo.PropertyType;
            }

            return null;
        }
    }
}
