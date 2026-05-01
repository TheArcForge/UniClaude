using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for reading and modifying asset import settings via the AssetImporter API.
    /// </summary>
    public static class AssetImportTools
    {
        /// <summary>
        /// Reads all import settings for an asset via its AssetImporter's SerializedObject.
        /// Works for any importer type (TextureImporter, ModelImporter, AudioImporter, etc.).
        /// </summary>
        [MCPTool("asset_get_import_settings", "Read import settings for any asset (textures, models, audio, etc.)")]
        public static MCPToolResult GetImportSettings(
            [MCPToolParam("Asset path (e.g. 'Assets/Art/Model.fbx')", required: true)] string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return MCPToolResult.Error(
                    $"No importer found for '{assetPath}'. The asset may not exist or has no configurable import settings.");

            var so = new SerializedObject(importer);
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

            return MCPToolResult.Success(new
            {
                assetPath,
                importerType = importer.GetType().Name,
                properties
            });
        }

        /// <summary>
        /// Sets import settings on any asset's importer and reimports.
        /// Uses SerializedObject for broad compatibility across importer types.
        /// </summary>
        [MCPTool("asset_set_import_settings", "Set import settings on any asset (textures, models, audio) and reimport")]
        public static MCPToolResult SetImportSettings(
            [MCPToolParam("Asset path (e.g. 'Assets/Art/Model.fbx')", required: true)] string assetPath,
            [MCPToolParam("JSON object of property names to values (e.g. '{\"m_MaxTextureSize\": 256}')", required: true)] string properties)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return MCPToolResult.Error(
                    $"No importer found for '{assetPath}'. The asset may not exist or has no configurable import settings.");

            JObject props;
            try
            {
                props = JObject.Parse(properties);
            }
            catch (JsonException ex)
            {
                return MCPToolResult.Error($"Invalid JSON: {ex.Message}");
            }

            var so = new SerializedObject(importer);
            var set = new List<string>();
            var errors = new List<string>();

            foreach (var kvp in props)
            {
                var prop = so.FindProperty(kvp.Key);
                if (prop == null)
                {
                    var validProps = ListImporterPropertyNames(so);
                    errors.Add($"Property '{kvp.Key}' not found. Valid properties: {string.Join(", ", validProps)}");
                    continue;
                }

                try
                {
                    SetPropertyValue(prop, kvp.Value);
                    set.Add(kvp.Key);
                }
                catch (System.Exception ex)
                {
                    errors.Add($"Failed to set '{kvp.Key}': {ex.Message}");
                }
            }

            if (set.Count > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            if (errors.Count > 0 && set.Count == 0)
                return MCPToolResult.Error(string.Join("\n", errors));

            return MCPToolResult.Success(new
            {
                assetPath,
                propertiesSet = set,
                errors
            });
        }

        /// <summary>
        /// Configures animation clip import settings (loop time, etc.) on a model/FBX asset.
        /// Handles the nested ModelImporterClipAnimation array that cannot be accessed via SerializedObject.
        /// </summary>
        [MCPTool("asset_set_clip_import_settings",
            "Configure animation clip settings (loopTime, loopPose, etc.) on an FBX/model asset and reimport")]
        public static MCPToolResult SetClipImportSettings(
            [MCPToolParam("FBX/model asset path", required: true)] string assetPath,
            [MCPToolParam("JSON array of clip configs: [{\"name\": \"Take 001\", \"loopTime\": true, ...}]", required: true)] string clips)
        {
            ClipConfig[] clipConfigs;
            try
            {
                clipConfigs = JsonConvert.DeserializeObject<ClipConfig[]>(clips);
            }
            catch (JsonException ex)
            {
                return MCPToolResult.Error($"Invalid JSON: {ex.Message}");
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                var anyImporter = AssetImporter.GetAtPath(assetPath);
                if (anyImporter == null)
                    return MCPToolResult.Error($"Asset not found at '{assetPath}'.");
                return MCPToolResult.Error(
                    $"Asset at '{assetPath}' uses {anyImporter.GetType().Name}, not ModelImporter. " +
                    "This tool only works on model/FBX files.");
            }

            var existingClips = importer.clipAnimations;
            if (existingClips == null || existingClips.Length == 0)
                existingClips = importer.defaultClipAnimations;

            if (existingClips == null || existingClips.Length == 0)
                return MCPToolResult.Error(
                    $"No animation clips found in '{assetPath}'. " +
                    "Ensure the model has animations and the rig type is set to Humanoid or Generic.");

            var clipNames = new List<string>();
            foreach (var c in existingClips)
                clipNames.Add(c.name);

            var updated = new List<string>();
            var errors = new List<string>();

            foreach (var config in clipConfigs)
            {
                var found = false;
                for (var i = 0; i < existingClips.Length; i++)
                {
                    if (existingClips[i].name != config.Name) continue;
                    found = true;

                    if (config.LoopTime.HasValue) existingClips[i].loopTime = config.LoopTime.Value;
                    if (config.LoopPose.HasValue) existingClips[i].loopPose = config.LoopPose.Value;
                    if (config.CycleOffset.HasValue) existingClips[i].cycleOffset = config.CycleOffset.Value;
                    if (config.FirstFrame.HasValue) existingClips[i].firstFrame = config.FirstFrame.Value;
                    if (config.LastFrame.HasValue) existingClips[i].lastFrame = config.LastFrame.Value;

                    updated.Add(config.Name);
                    break;
                }

                if (!found)
                    errors.Add($"Clip '{config.Name}' not found. Available clips: {string.Join(", ", clipNames)}");
            }

            if (updated.Count > 0)
            {
                importer.clipAnimations = existingClips;
                importer.SaveAndReimport();
            }

            if (errors.Count > 0 && updated.Count == 0)
                return MCPToolResult.Error(string.Join("\n", errors));

            return MCPToolResult.Success(new { assetPath, updatedClips = updated, errors });
        }

        /// <summary>
        /// Data class for deserializing per-clip animation import config from JSON.
        /// </summary>
        class ClipConfig
        {
            [JsonProperty("name")] public string Name;
            [JsonProperty("loopTime")] public bool? LoopTime;
            [JsonProperty("loopPose")] public bool? LoopPose;
            [JsonProperty("cycleOffset")] public float? CycleOffset;

            [JsonProperty("firstFrame")] public float? FirstFrame;
            [JsonProperty("lastFrame")] public float? LastFrame;
        }

        /// <summary>
        /// Reads a SerializedProperty value as a string for display.
        /// Handles common types; returns a type hint for unsupported ones.
        /// </summary>
        static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length
                        ? $"{prop.enumNames[prop.enumValueIndex]} ({prop.enumValueIndex})"
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? AssetDatabase.GetAssetPath(prop.objectReferenceValue)
                        : null;
                default:
                    return $"<{prop.propertyType}>";
            }
        }

        /// <summary>
        /// Sets a SerializedProperty value from a JToken.
        /// </summary>
        static void SetPropertyValue(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.Value<int>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.Value<bool>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.Value<float>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.Value<string>();
                    break;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.Integer)
                        prop.enumValueIndex = value.Value<int>();
                    else
                    {
                        var name = value.Value<string>();
                        var idx = System.Array.IndexOf(prop.enumNames, name);
                        if (idx < 0)
                            throw new System.ArgumentException(
                                $"Invalid enum value '{name}'. Valid: {string.Join(", ", prop.enumNames)}");
                        prop.enumValueIndex = idx;
                    }
                    break;
                default:
                    throw new System.ArgumentException($"Unsupported property type: {prop.propertyType}");
            }
        }

        /// <summary>
        /// Lists all visible serialized property names on an importer.
        /// </summary>
        static string[] ListImporterPropertyNames(SerializedObject so)
        {
            var names = new List<string>();
            var iterator = so.GetIterator();
            if (iterator.NextVisible(true))
            {
                do { names.Add(iterator.name); } while (iterator.NextVisible(false));
            }
            return names.ToArray();
        }
    }
}
