using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor
{
    /// <summary>
    /// Editor utility to add BoxColliders to all wall objects in the scene.
    /// </summary>
    public static class WallColliderAdder
    {
        [MenuItem("ArcForge/Add BoxColliders to All Walls")]
        public static void AddBoxCollidersToWalls()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            int colliderCount = 0;
            
            Debug.Log("=== Adding BoxColliders to Wall Objects ===");
            
            foreach (var rootObject in rootObjects)
            {
                colliderCount += AddCollidersToWallsRecursive(rootObject.transform);
            }
            
            Debug.Log($"Added BoxColliders to {colliderCount} wall object(s)");
            
            if (colliderCount == 0)
            {
                Debug.Log("No wall objects found. Wall objects should have names containing: 'wall', 'Wall', 'barrier', 'fence', or be Cube primitives");
            }
        }
        
        private static int AddCollidersToWallsRecursive(Transform transform)
        {
            var gameObject = transform.gameObject;
            int count = 0;
            
            // Check if this looks like a wall object (common naming patterns)
            var name = gameObject.name.ToLower();
            bool isWall = name.Contains("wall") || 
                         name.Contains("barrier") || 
                         name.Contains("fence") ||
                         name.Contains("cube");  // Many walls are just cubes
            
            if (isWall)
            {
                // Check if it already has a BoxCollider
                var existingCollider = gameObject.GetComponent<BoxCollider>();
                if (existingCollider == null)
                {
                    // Add BoxCollider component with Undo support
                    var boxCollider = Undo.AddComponent<BoxCollider>(gameObject);
                    
                    // Try to set reasonable collider bounds based on the renderer
                    var renderer = gameObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Undo.RecordObject(boxCollider, "Set BoxCollider bounds");
                        var bounds = renderer.bounds;
                        var localBounds = gameObject.transform.InverseTransformBounds(bounds);
                        boxCollider.center = localBounds.center;
                        boxCollider.size = localBounds.size;
                    }
                    
                    Debug.Log($"Added BoxCollider to: {gameObject.name}");
                    count++;
                }
                else
                {
                    Debug.Log($"BoxCollider already exists on: {gameObject.name}");
                }
            }
            
            // Recurse into children
            for (int i = 0; i < transform.childCount; i++)
            {
                count += AddCollidersToWallsRecursive(transform.GetChild(i));
            }
            
            return count;
        }
        
        [MenuItem("ArcForge/Remove BoxColliders from All Walls")]
        public static void RemoveBoxCollidersFromWalls()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            int removedCount = 0;
            
            Debug.Log("=== Removing BoxColliders from Wall Objects ===");
            
            foreach (var rootObject in rootObjects)
            {
                removedCount += RemoveCollidersFromWallsRecursive(rootObject.transform);
            }
            
            Debug.Log($"Removed BoxColliders from {removedCount} wall object(s)");
        }
        
        private static int RemoveCollidersFromWallsRecursive(Transform transform)
        {
            var gameObject = transform.gameObject;
            int count = 0;
            
            // Check if this looks like a wall object
            var name = gameObject.name.ToLower();
            bool isWall = name.Contains("wall") || 
                         name.Contains("barrier") || 
                         name.Contains("fence") ||
                         name.Contains("cube");
            
            if (isWall)
            {
                var boxCollider = gameObject.GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    Undo.DestroyObjectImmediate(boxCollider);
                    Debug.Log($"Removed BoxCollider from: {gameObject.name}");
                    count++;
                }
            }
            
            // Recurse into children
            for (int i = 0; i < transform.childCount; i++)
            {
                count += RemoveCollidersFromWallsRecursive(transform.GetChild(i));
            }
            
            return count;
        }
        
        [MenuItem("ArcForge/List Wall Objects in Scene")]
        public static void ListWallObjects()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            
            Debug.Log("=== Wall Objects in Scene ===");
            foreach (var rootObject in rootObjects)
            {
                ListWallObjectsRecursive(rootObject.transform, 0);
            }
        }
        
        private static void ListWallObjectsRecursive(Transform transform, int depth)
        {
            var gameObject = transform.gameObject;
            var name = gameObject.name.ToLower();
            bool isWall = name.Contains("wall") || 
                         name.Contains("barrier") || 
                         name.Contains("fence") ||
                         name.Contains("cube");
            
            if (isWall)
            {
                var indent = new string(' ', depth * 2);
                var boxCollider = gameObject.GetComponent<BoxCollider>();
                var colliderInfo = boxCollider != null ? " [Has BoxCollider]" : " [No BoxCollider]";
                
                Debug.Log($"{indent}- {transform.name}{colliderInfo}");
            }
            
            for (int i = 0; i < transform.childCount; i++)
            {
                ListWallObjectsRecursive(transform.GetChild(i), depth + 1);
            }
        }
    }
}

// Extension method to help with bounds transformation
public static class TransformExtensions
{
    public static Bounds InverseTransformBounds(this Transform transform, Bounds bounds)
    {
        var center = transform.InverseTransformPoint(bounds.center);
        var size = transform.InverseTransformVector(bounds.size);
        
        // Make sure size components are positive
        size.x = Mathf.Abs(size.x);
        size.y = Mathf.Abs(size.y);
        size.z = Mathf.Abs(size.z);
        
        return new Bounds(center, size);
    }
}
