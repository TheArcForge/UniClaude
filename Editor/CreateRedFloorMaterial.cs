using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor
{
    /// <summary>
    /// Editor utility to create a red material and apply it to floor objects in the scene.
    /// </summary>
    public static class RedFloorMaterialCreator
    {
        [MenuItem("ArcForge/Create Red Floor Material")]
        public static void CreateAndApplyRedMaterial()
        {
            // Create the Materials folder if it doesn't exist
            var materialsPath = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(materialsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            
            // Create a new red material
            var material = new Material(Shader.Find("Standard"));
            material.name = "RedFloorMaterial";
            material.color = Color.red;
            
            // Save the material asset
            var materialPath = materialsPath + "/RedFloorMaterial.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Created red material at: {materialPath}");
            
            // Find and apply to floor objects
            ApplyToFloorObjects(material);
            
            // Select the material in the project window
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
        }
        
        private static void ApplyToFloorObjects(Material material)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            foreach (var rootObject in rootObjects)
            {
                ApplyToFloorObjectsRecursive(rootObject.transform, material);
            }
        }
        
        private static void ApplyToFloorObjectsRecursive(Transform transform, Material material)
        {
            var gameObject = transform.gameObject;
            
            // Check if this looks like a floor object (common naming patterns)
            var name = gameObject.name.ToLower();
            if (name.Contains("floor") || name.Contains("ground") || name.Contains("plane"))
            {
                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Apply Red Material to Floor");
                    renderer.material = material;
                    Debug.Log($"Applied red material to: {gameObject.name}");
                }
            }
            
            // Recurse into children
            for (int i = 0; i < transform.childCount; i++)
            {
                ApplyToFloorObjectsRecursive(transform.GetChild(i), material);
            }
        }
        
        [MenuItem("ArcForge/List Scene Objects")]
        public static void ListSceneObjects()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            Debug.Log("=== Scene Objects ===");
            foreach (var rootObject in rootObjects)
            {
                ListObjectsRecursive(rootObject.transform, 0);
            }
        }
        
        private static void ListObjectsRecursive(Transform transform, int depth)
        {
            var indent = new string(' ', depth * 2);
            var renderer = transform.GetComponent<Renderer>();
            var rendererInfo = renderer != null ? $" [Renderer: {renderer.GetType().Name}]" : "";
            
            Debug.Log($"{indent}- {transform.name}{rendererInfo}");
            
            for (int i = 0; i < transform.childCount; i++)
            {
                ListObjectsRecursive(transform.GetChild(i), depth + 1);
            }
        }
    }
}
