using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Resolves GameObject hierarchy paths including inactive objects.
    /// Replaces GameObject.Find() which skips inactive GameObjects.
    /// </summary>
    public static class GameObjectResolver
    {
        /// <summary>
        /// Finds a GameObject by hierarchy path in the active scene, including inactive objects.
        /// </summary>
        /// <param name="path">Hierarchy path (e.g. "Canvas/Panel/Button") or simple name.</param>
        /// <returns>The found GameObject, or null if not found.</returns>
        public static GameObject FindByPath(string path)
        {
            return FindByPath(SceneManager.GetActiveScene(), path);
        }

        /// <summary>
        /// Finds a GameObject by hierarchy path in a specific scene, including inactive objects.
        /// </summary>
        /// <param name="scene">The scene to search in.</param>
        /// <param name="path">Hierarchy path (e.g. "Canvas/Panel/Button") or simple name.</param>
        /// <returns>The found GameObject, or null if not found.</returns>
        public static GameObject FindByPath(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var segments = path.Split('/');
            var roots = scene.GetRootGameObjects();

            // Find the root by name
            GameObject current = null;
            foreach (var root in roots)
            {
                if (root.name == segments[0])
                {
                    current = root;
                    break;
                }
            }

            if (current == null)
                return null;

            // Walk remaining segments using Transform.Find (works on inactive children)
            for (int i = 1; i < segments.Length; i++)
            {
                var child = current.transform.Find(segments[i]);
                if (child == null)
                    return null;
                current = child.gameObject;
            }

            return current;
        }
    }
}
