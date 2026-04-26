using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace UniClaude.Editor.Tools
{
    /// <summary>
    /// Editor utility to create and apply a night sky skybox material.
    /// </summary>
    public static class NightSkyboxCreator
    {
        /// <summary>
        /// Menu item to create a night sky skybox material and set it as the scene's skybox.
        /// </summary>
        [MenuItem("ArcForge/Utilities/Set Night Sky Skybox")]
        public static void CreateNightSkybox()
        {
            if (!EditorUtility.DisplayDialog(
                "Create Night Sky Skybox", 
                "This will create a night sky skybox material and set it as the current scene's skybox. Continue?", 
                "Create", 
                "Cancel"))
            {
                return;
            }

            var skyboxMaterial = CreateNightSkyboxMaterial();
            SetSceneSkybox(skyboxMaterial);
            
            Debug.Log($"Created and applied night sky skybox material: {skyboxMaterial.name}");
            
            EditorUtility.DisplayDialog("Skybox Applied", 
                $"Night sky skybox '{skyboxMaterial.name}' has been created and set as the scene's skybox.", 
                "OK");
        }

        /// <summary>
        /// Creates a night sky skybox material with a dark blue gradient effect.
        /// </summary>
        /// <returns>The created skybox material.</returns>
        public static Material CreateNightSkyboxMaterial()
        {
            // Create the Materials folder if it doesn't exist
            var materialsPath = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(materialsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            // Create a new skybox material using the Procedural skybox shader
            var material = new Material(Shader.Find("Skybox/Procedural"));
            material.name = "NightSkyboxMaterial";
            
            // Configure the material for a night sky appearance
            ConfigureNightSkyMaterial(material);
            
            // Save the material asset
            var materialPath = materialsPath + "/NightSkyboxMaterial.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            
            // Select the material in the project window
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            
            return material;
        }

        /// <summary>
        /// Configures a material to look like a night sky.
        /// </summary>
        /// <param name="material">The skybox material to configure.</param>
        static void ConfigureNightSkyMaterial(Material material)
        {
            // Sun settings (make it very dim or hidden)
            material.SetFloat("_SunDisk", 0); // No sun disk
            material.SetFloat("_SunSize", 0.04f); // Small sun size
            material.SetFloat("_SunSizeConvergence", 5f);
            
            // Atmosphere settings for night
            material.SetFloat("_AtmosphereThickness", 1.0f);
            material.SetColor("_SkyTint", new Color(0.2f, 0.2f, 0.4f, 1f)); // Dark blue tint
            material.SetColor("_GroundColor", new Color(0.05f, 0.05f, 0.1f, 1f)); // Very dark ground
            
            // Exposure settings to make it darker
            material.SetFloat("_Exposure", 0.3f); // Low exposure for night effect
        }

        /// <summary>
        /// Sets the specified material as the scene's skybox in the Lighting settings.
        /// </summary>
        /// <param name="skyboxMaterial">The skybox material to apply.</param>
        public static void SetSceneSkybox(Material skyboxMaterial)
        {
            // Record the change for undo
            Undo.RecordObject(RenderSettings.skybox, "Change Scene Skybox");
            
            // Set the skybox material
            RenderSettings.skybox = skyboxMaterial;
            
            // Force the scene view to update
            SceneView.RepaintAll();
            
            // Mark the scene as dirty
            EditorUtility.SetDirty(RenderSettings.skybox);
        }

        /// <summary>
        /// Menu item to create a starfield skybox material.
        /// </summary>
        [MenuItem("ArcForge/Utilities/Set Starfield Skybox")]
        public static void CreateStarfieldSkybox()
        {
            if (!EditorUtility.DisplayDialog(
                "Create Starfield Skybox", 
                "This will create a starfield skybox material and set it as the current scene's skybox. Continue?", 
                "Create", 
                "Cancel"))
            {
                return;
            }

            var skyboxMaterial = CreateStarfieldSkyboxMaterial();
            SetSceneSkybox(skyboxMaterial);
            
            Debug.Log($"Created and applied starfield skybox material: {skyboxMaterial.name}");
            
            EditorUtility.DisplayDialog("Skybox Applied", 
                $"Starfield skybox '{skyboxMaterial.name}' has been created and set as the scene's skybox.", 
                "OK");
        }

        /// <summary>
        /// Creates a starfield skybox material with a black space background.
        /// </summary>
        /// <returns>The created skybox material.</returns>
        public static Material CreateStarfieldSkyboxMaterial()
        {
            // Create the Materials folder if it doesn't exist
            var materialsPath = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(materialsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            // Create a new skybox material using the Procedural skybox shader
            var material = new Material(Shader.Find("Skybox/Procedural"));
            material.name = "StarfieldSkyboxMaterial";
            
            // Configure the material for a starfield appearance
            ConfigureStarfieldMaterial(material);
            
            // Save the material asset
            var materialPath = materialsPath + "/StarfieldSkyboxMaterial.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            
            // Select the material in the project window
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            
            return material;
        }

        /// <summary>
        /// Configures a material to look like a starfield in deep space.
        /// </summary>
        /// <param name="material">The skybox material to configure.</param>
        static void ConfigureStarfieldMaterial(Material material)
        {
            // Sun settings (completely hidden)
            material.SetFloat("_SunDisk", 0); // No sun disk
            material.SetFloat("_SunSize", 0.01f); // Very small sun
            material.SetFloat("_SunSizeConvergence", 10f);
            
            // Atmosphere settings for deep space
            material.SetFloat("_AtmosphereThickness", 0.1f); // Minimal atmosphere
            material.SetColor("_SkyTint", new Color(0.05f, 0.05f, 0.1f, 1f)); // Very dark blue-black
            material.SetColor("_GroundColor", Color.black); // Pure black ground
            
            // Very low exposure for deep space effect
            material.SetFloat("_Exposure", 0.1f);
        }

        /// <summary>
        /// Validation function for the skybox menu items.
        /// Only shows the menu when we're not playing.
        /// </summary>
        [MenuItem("ArcForge/Utilities/Set Night Sky Skybox", true)]
        [MenuItem("ArcForge/Utilities/Set Starfield Skybox", true)]
        public static bool ValidateCreateSkybox()
        {
            return !Application.isPlaying;
        }
    }
}
