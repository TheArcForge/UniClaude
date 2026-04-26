using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.Tools
{
    /// <summary>
    /// Editor utility to find and delete empty GameObjects (those with only Transform components).
    /// </summary>
    public static class EmptyGameObjectCleaner
    {
        /// <summary>
        /// Menu item to delete all empty GameObjects in the active scene.
        /// </summary>
        [MenuItem("ArcForge/Utilities/Delete Empty GameObjects")]
        public static void DeleteEmptyGameObjects()
        {
            if (!EditorUtility.DisplayDialog(
                "Delete Empty GameObjects", 
                "This will delete all GameObjects in the active scene that only have Transform components. This action can be undone. Continue?", 
                "Delete", 
                "Cancel"))
            {
                return;
            }

            var emptyObjects = FindEmptyGameObjects();
            
            if (emptyObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("No Empty GameObjects", "No empty GameObjects found in the active scene.", "OK");
                return;
            }

            // Group the deletion for a single undo operation
            Undo.IncrementCurrentGroup();
            var undoGroupIndex = Undo.GetCurrentGroup();
            
            foreach (var go in emptyObjects)
            {
                Undo.DestroyObjectImmediate(go);
            }
            
            Undo.SetCurrentGroupName($"Delete {emptyObjects.Count} Empty GameObjects");
            Undo.CollapseUndoOperations(undoGroupIndex);
            
            Debug.Log($"Deleted {emptyObjects.Count} empty GameObjects from scene '{SceneManager.GetActiveScene().name}'");
            
            EditorUtility.DisplayDialog("Cleanup Complete", 
                $"Deleted {emptyObjects.Count} empty GameObjects.\n\nUse Ctrl/Cmd+Z to undo if needed.", 
                "OK");
        }

        /// <summary>
        /// Finds all empty GameObjects in the active scene.
        /// An empty GameObject is one that only has a Transform component.
        /// </summary>
        /// <returns>List of empty GameObjects found.</returns>
        public static List<GameObject> FindEmptyGameObjects()
        {
            var scene = SceneManager.GetActiveScene();
            var emptyObjects = new List<GameObject>();
            
            // Get all GameObjects in the scene, including inactive ones
            var allObjects = scene.GetRootGameObjects()
                .SelectMany(GetAllChildrenRecursive)
                .Concat(scene.GetRootGameObjects())
                .ToList();
            
            foreach (var go in allObjects)
            {
                if (IsEmptyGameObject(go))
                {
                    emptyObjects.Add(go);
                }
            }
            
            return emptyObjects;
        }

        /// <summary>
        /// Checks if a GameObject is considered "empty" (only has Transform component).
        /// </summary>
        /// <param name="go">The GameObject to check.</param>
        /// <returns>True if the GameObject only has a Transform component.</returns>
        static bool IsEmptyGameObject(GameObject go)
        {
            if (go == null) return false;
            
            var components = go.GetComponents<Component>();
            
            // An empty GameObject should only have a Transform component
            // Filter out null components (can happen with missing script references)
            var validComponents = components.Where(c => c != null).ToArray();
            
            // Check if it only has a Transform component
            return validComponents.Length == 1 && validComponents[0] is Transform;
        }

        /// <summary>
        /// Recursively gets all children of a GameObject.
        /// </summary>
        /// <param name="parent">The parent GameObject.</param>
        /// <returns>All child GameObjects recursively.</returns>
        static IEnumerable<GameObject> GetAllChildrenRecursive(GameObject parent)
        {
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                var child = parent.transform.GetChild(i).gameObject;
                yield return child;
                
                foreach (var grandchild in GetAllChildrenRecursive(child))
                {
                    yield return grandchild;
                }
            }
        }

        /// <summary>
        /// Validation function for the menu item.
        /// Only shows the menu when we're not playing and have an active scene.
        /// </summary>
        [MenuItem("ArcForge/Utilities/Delete Empty GameObjects", true)]
        public static bool ValidateDeleteEmptyGameObjects()
        {
            return !Application.isPlaying && SceneManager.GetActiveScene().isLoaded;
        }
    }
}
