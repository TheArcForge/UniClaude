using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="PrefabTools"/> MCP tools covering prefab creation,
    /// instantiation, override application, and hierarchy inspection.
    /// Uses a temporary directory under Assets for prefab assets, cleaned in TearDown.
    /// </summary>
    public class PrefabToolsTests
    {
        /// <summary>
        /// Project-relative path to the temp directory for test prefab assets.
        /// Must be under Assets/ so that AssetDatabase can find them.
        /// </summary>
        const string TempAssetDir = "Assets/UniClaudeTestTemp";

        /// <summary>
        /// Tracks scene GameObjects created during tests for cleanup.
        /// </summary>
        List<GameObject> _tempObjects;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _tempObjects = new List<GameObject>();
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!AssetDatabase.IsValidFolder(TempAssetDir))
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _tempObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _tempObjects.Clear();

            // Delete temp asset folder and refresh
            if (AssetDatabase.IsValidFolder(TempAssetDir))
            {
                AssetDatabase.DeleteAsset(TempAssetDir);
                AssetDatabase.Refresh();
            }

            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        /// <summary>
        /// Creates a temporary GameObject that will be cleaned up in TearDown.
        /// </summary>
        /// <param name="name">Name for the temporary GameObject.</param>
        /// <returns>The created GameObject.</returns>
        GameObject CreateTemp(string name)
        {
            var go = new GameObject(name);
            _tempObjects.Add(go);
            return go;
        }

        /// <summary>
        /// Creates a temporary prefab asset from a GameObject and returns the asset path.
        /// The source GameObject is destroyed after saving.
        /// </summary>
        /// <param name="name">Name for the prefab.</param>
        /// <returns>The project-relative asset path of the saved prefab.</returns>
        string CreateTempPrefab(string name)
        {
            var go = new GameObject(name);
            var path = TempAssetDir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        [Test]
        public void CreatePrefab_SavesAsset()
        {
            var go = CreateTemp("PrefabToolsTest_Create");
            var assetPath = TempAssetDir + "/PrefabToolsTest_Create.prefab";

            var result = PrefabTools.CreatePrefab("PrefabToolsTest_Create", assetPath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain(assetPath));

            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.IsNotNull(loaded, "Prefab asset should exist at the specified path");
        }

        [Test]
        public void CreatePrefab_GONotFound_Error()
        {
            var marker = CreateTemp("PrefabToolsTest_Marker");
            var assetPath = TempAssetDir + "/ShouldNotExist.prefab";

            var result = PrefabTools.CreatePrefab("NonExistentGO_PrefabTest_12345", assetPath);

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("GameObject not found"));
            Assert.That(result.Text, Does.Contain("NonExistentGO_PrefabTest_12345"));
            Assert.That(result.Text, Does.Contain("Root objects in scene:"));
            Assert.That(result.Text, Does.Contain("PrefabToolsTest_Marker"));
        }

        [Test]
        public void InstantiatePrefab_CreatesInScene()
        {
            var prefabPath = CreateTempPrefab("PrefabToolsTest_Inst");

            var result = PrefabTools.InstantiatePrefab(prefabPath, null);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("PrefabToolsTest_Inst"));

            var found = GameObject.Find("PrefabToolsTest_Inst");
            Assert.IsNotNull(found, "Instantiated prefab should exist in scene");
            _tempObjects.Add(found);
        }

        [Test]
        public void InstantiatePrefab_WithParent()
        {
            var parent = CreateTemp("PrefabToolsTest_InstParent");
            var prefabPath = CreateTempPrefab("PrefabToolsTest_InstChild");

            var result = PrefabTools.InstantiatePrefab(prefabPath, "PrefabToolsTest_InstParent");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");

            var found = GameObject.Find("PrefabToolsTest_InstParent/PrefabToolsTest_InstChild");
            Assert.IsNotNull(found, "Instantiated prefab should exist under parent");
            Assert.AreEqual(parent.transform, found.transform.parent);
            _tempObjects.Add(found);
        }

        [Test]
        public void InstantiatePrefab_PrefabNotFound_Error()
        {
            var result = PrefabTools.InstantiatePrefab(
                TempAssetDir + "/NonExistent_99999.prefab", null);

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("Prefab not found"));
            Assert.That(result.Text, Does.Contain("NonExistent_99999.prefab"));
        }

        [Test]
        public void ApplyPrefabOverrides_Applies()
        {
            // Create a prefab from a GO
            var sourceGo = new GameObject("PrefabToolsTest_Apply");
            var prefabPath = TempAssetDir + "/PrefabToolsTest_Apply.prefab";
            PrefabUtility.SaveAsPrefabAsset(sourceGo, prefabPath);
            Object.DestroyImmediate(sourceGo);

            // Instantiate as a prefab instance
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            _tempObjects.Add(instance);

            // Modify the instance (add a component as override)
            instance.AddComponent<BoxCollider>();

            // Verify the source prefab does NOT yet have the BoxCollider
            var prefabBefore = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNull(prefabBefore.GetComponent<BoxCollider>(),
                "Precondition: source prefab should not have BoxCollider before apply");

            var result = PrefabTools.ApplyPrefabOverrides("PrefabToolsTest_Apply");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("PrefabToolsTest_Apply"));

            // Reload and verify the source prefab now has the BoxCollider
            var prefabAfter = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefabAfter.GetComponent<BoxCollider>(),
                "Source prefab should have BoxCollider after apply");
        }

        [Test]
        public void GetPrefabContents_ReturnsHierarchy()
        {
            // Create a prefab with children
            var root = new GameObject("PrefabToolsTest_Contents");
            var childA = new GameObject("ChildA");
            childA.transform.SetParent(root.transform);
            var childB = new GameObject("ChildB");
            childB.transform.SetParent(root.transform);
            childB.AddComponent<BoxCollider>();
            var grandchild = new GameObject("GrandChild");
            grandchild.transform.SetParent(childA.transform);

            var prefabPath = TempAssetDir + "/PrefabToolsTest_Contents.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var result = PrefabTools.GetPrefabContents(prefabPath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.That(result.Text, Does.Contain("PrefabToolsTest_Contents"));
            Assert.That(result.Text, Does.Contain("ChildA"));
            Assert.That(result.Text, Does.Contain("ChildB"));
            Assert.That(result.Text, Does.Contain("GrandChild"));
            Assert.That(result.Text, Does.Contain("BoxCollider"));
        }

        [Test]
        public void GetPrefabContents_NotFound_Error()
        {
            var result = PrefabTools.GetPrefabContents(
                TempAssetDir + "/NonExistentPrefab_77777.prefab");

            Assert.IsTrue(result.IsError);
            Assert.That(result.Text, Does.Contain("Prefab not found"));
            Assert.That(result.Text, Does.Contain("NonExistentPrefab_77777.prefab"));
        }
    }
}
