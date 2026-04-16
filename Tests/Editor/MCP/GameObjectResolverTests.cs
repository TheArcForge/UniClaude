using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="GameObjectResolver"/> inactive-aware path resolution.
    /// </summary>
    public class GameObjectResolverTests
    {
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        [Test]
        public void FindByPath_ActiveRoot_ReturnsGameObject()
        {
            var go = new GameObject("MyRoot");
            var found = GameObjectResolver.FindByPath("MyRoot");
            Assert.AreEqual(go, found);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FindByPath_ActiveChild_ReturnsGameObject()
        {
            var parent = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);

            var found = GameObjectResolver.FindByPath("Parent/Child");
            Assert.AreEqual(child, found);

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void FindByPath_InactiveRoot_ReturnsGameObject()
        {
            var go = new GameObject("InactiveRoot");
            go.SetActive(false);

            var found = GameObjectResolver.FindByPath("InactiveRoot");
            Assert.AreEqual(go, found);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FindByPath_InactiveChild_ReturnsGameObject()
        {
            var parent = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);
            child.SetActive(false);

            var found = GameObjectResolver.FindByPath("Parent/Child");
            Assert.AreEqual(child, found);

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void FindByPath_InactiveParent_ReturnsChild()
        {
            var parent = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);
            parent.SetActive(false);

            var found = GameObjectResolver.FindByPath("Parent/Child");
            Assert.AreEqual(child, found);

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void FindByPath_DeepPath_ReturnsGameObject()
        {
            var a = new GameObject("A");
            var b = new GameObject("B");
            var c = new GameObject("C");
            b.transform.SetParent(a.transform);
            c.transform.SetParent(b.transform);

            var found = GameObjectResolver.FindByPath("A/B/C");
            Assert.AreEqual(c, found);

            Object.DestroyImmediate(a);
        }

        [Test]
        public void FindByPath_NullPath_ReturnsNull()
        {
            Assert.IsNull(GameObjectResolver.FindByPath(null));
        }

        [Test]
        public void FindByPath_EmptyPath_ReturnsNull()
        {
            Assert.IsNull(GameObjectResolver.FindByPath(""));
        }

        [Test]
        public void FindByPath_NotFound_ReturnsNull()
        {
            var found = GameObjectResolver.FindByPath("DoesNotExist");
            Assert.IsNull(found);
        }

        [Test]
        public void FindByPath_ChildNotFound_ReturnsNull()
        {
            var parent = new GameObject("Parent");
            var found = GameObjectResolver.FindByPath("Parent/NoSuchChild");
            Assert.IsNull(found);
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void FindByPath_WithExplicitScene_ReturnsGameObject()
        {
            var go = new GameObject("SceneTest");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var found = GameObjectResolver.FindByPath(scene, "SceneTest");
            Assert.AreEqual(go, found);
            Object.DestroyImmediate(go);
        }
    }
}
