using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for PrefabTools extension methods (prefab_edit_property, prefab_open_editing,
    /// prefab_save_editing, prefab_create_variant).
    /// </summary>
    public class PrefabToolsExtensionsTests
    {
        const string TestFolder = "Assets/UniClaudeTestTemp";
        string _prefabPath;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");

            var tempGO = new GameObject("PrefabExtTestGO");
            tempGO.AddComponent<Rigidbody>();
            _prefabPath = $"{TestFolder}/TestPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(tempGO, _prefabPath);
            Object.DestroyImmediate(tempGO);
        }

        [TearDown]
        public void TearDown()
        {
            PrefabTools.ClosePrefabEditingSession();

            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        [Test]
        public void PrefabEditProperty_SetsPropertyInPrefab()
        {
            var result = PrefabTools.EditPrefabProperty(
                _prefabPath, "Rigidbody", "m_Mass", "25");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            var rb = prefab.GetComponent<Rigidbody>();
            Assert.AreEqual(25f, rb.mass, 0.01f);
        }

        [Test]
        public void PrefabEditProperty_PrefabNotFound_ReturnsError()
        {
            var result = PrefabTools.EditPrefabProperty(
                "Assets/NonExistent.prefab", "Rigidbody", "m_Mass", "10");

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void PrefabOpenAndSave_EditingSession_Works()
        {
            var openResult = PrefabTools.OpenPrefabEditing(_prefabPath);
            Assert.IsFalse(openResult.IsError, $"Open failed: {openResult.Text}");

            var saveResult = PrefabTools.SavePrefabEditing();
            Assert.IsFalse(saveResult.IsError, $"Save failed: {saveResult.Text}");
        }

        [Test]
        public void PrefabOpenEditing_AlreadyOpen_ReturnsError()
        {
            PrefabTools.OpenPrefabEditing(_prefabPath);

            var result = PrefabTools.OpenPrefabEditing(_prefabPath);

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("already open", result.Text);

            PrefabTools.SavePrefabEditing();
        }

        [Test]
        public void PrefabSaveEditing_NoneOpen_ReturnsError()
        {
            var result = PrefabTools.SavePrefabEditing();

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("No prefab", result.Text);
        }

        [Test]
        public void PrefabCreateVariant_CreatesVariant()
        {
            var variantPath = $"{TestFolder}/TestPrefabVariant.prefab";

            var result = PrefabTools.CreatePrefabVariant(_prefabPath, variantPath);

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath));
        }

        [Test]
        public void PrefabCreateVariant_BaseNotFound_ReturnsError()
        {
            var result = PrefabTools.CreatePrefabVariant(
                "Assets/NonExistent.prefab", $"{TestFolder}/Variant.prefab");

            Assert.IsTrue(result.IsError);
        }
    }
}
