using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="MaterialTools"/> MCP tools.
    /// </summary>
    public class MaterialToolsTests
    {
        const string TestFolder = "Assets/UniClaudeTestTemp";
        string _testMatPath;
        GameObject _tempGO;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");

            _testMatPath = $"{TestFolder}/TestMat.mat";
            _tempGO = new GameObject("MatTestGO");
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempGO != null) Object.DestroyImmediate(_tempGO);
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        [Test]
        public void MaterialCreate_CreatesAsset()
        {
            var result = MaterialTools.CreateMaterial(_testMatPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(_testMatPath));
        }

        [Test]
        public void MaterialCreate_CustomShader()
        {
            var result = MaterialTools.CreateMaterial(_testMatPath, "Unlit/Color");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_testMatPath);
            Assert.AreEqual("Unlit/Color", mat.shader.name);
        }

        [Test]
        public void MaterialCreate_InvalidShader_ReturnsError()
        {
            var result = MaterialTools.CreateMaterial(_testMatPath, "NonExistent/Shader999");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void MaterialSetProperty_SetsColor()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            var result = MaterialTools.SetMaterialProperty(
                _testMatPath, "_Color", "{\"r\":1,\"g\":0,\"b\":0,\"a\":1}");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_testMatPath);
            Assert.AreEqual(1f, mat.color.r, 0.01f);
            Assert.AreEqual(0f, mat.color.g, 0.01f);
        }

        [Test]
        public void MaterialSetProperty_SetsFloat()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            var result = MaterialTools.SetMaterialProperty(
                _testMatPath, "_Glossiness", "0.75", "float");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_testMatPath);
            Assert.AreEqual(0.75f, mat.GetFloat("_Glossiness"), 0.01f);
        }

        [Test]
        public void MaterialGetProperties_ReturnsProperties()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            var result = MaterialTools.GetMaterialProperties(_testMatPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("_Color", result.Text);
        }

        [Test]
        public void MaterialGetProperties_NotFound_ReturnsError()
        {
            var result = MaterialTools.GetMaterialProperties("Assets/NonExistent.mat");
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void MaterialAssign_AssignsToRenderer()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            _tempGO.AddComponent<MeshRenderer>();
            var result = MaterialTools.AssignMaterial("MatTestGO", _testMatPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var renderer = _tempGO.GetComponent<MeshRenderer>();
            Assert.AreEqual("TestMat", renderer.sharedMaterial.name);
        }

        [Test]
        public void MaterialAssign_NoRenderer_ReturnsError()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            var result = MaterialTools.AssignMaterial("MatTestGO", _testMatPath);
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("Renderer", result.Text);
        }

        [Test]
        public void MaterialDuplicate_CreatesNewAsset()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            var destPath = $"{TestFolder}/TestMatCopy.mat";
            var result = MaterialTools.DuplicateMaterial(_testMatPath, destPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(destPath));
        }

        [Test]
        public void MaterialSwapShader_ChangesShader()
        {
            MaterialTools.CreateMaterial(_testMatPath);
            var result = MaterialTools.SwapShader(_testMatPath, "Unlit/Color");
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_testMatPath);
            Assert.AreEqual("Unlit/Color", mat.shader.name);
        }
    }
}
