using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="SceneManagementTools"/> MCP tools.
    /// </summary>
    public class SceneManagementToolsTests
    {
        const string TestFolder = "Assets/UniClaudeTestTemp";
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        [Test]
        public void SceneSave_SaveAs_CreatesFile()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var savePath = $"{TestFolder}/SaveTest.unity";
            var result = SceneManagementTools.SaveScene(savePath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsTrue(AssetDatabase.LoadAssetAtPath<SceneAsset>(savePath) != null);
        }

        [Test]
        public void SceneCreate_CreatesNewScene()
        {
            var scenePath = $"{TestFolder}/NewScene.unity";
            var result = SceneManagementTools.CreateScene(scenePath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath));
        }

        [Test]
        public void SceneOpen_OpensScene()
        {
            var scenePath = $"{TestFolder}/OpenTest.unity";
            SceneManagementTools.CreateScene(scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = SceneManagementTools.OpenScene(scenePath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
        }

        [Test]
        public void SceneOpen_NotFound_ReturnsError()
        {
            var result = SceneManagementTools.OpenScene("Assets/NonExistent.unity");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void SceneDuplicate_CreatesNewSceneAsset()
        {
            var sourcePath = $"{TestFolder}/DupSource.unity";
            var destPath = $"{TestFolder}/DupDest.unity";
            SceneManagementTools.CreateScene(sourcePath);
            var result = SceneManagementTools.DuplicateScene(sourcePath, destPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(destPath));
        }

        [Test]
        public void SceneListBuild_ReturnsResult()
        {
            var result = SceneManagementTools.ListBuildScenes();
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("scenes", result.Text);
        }

        [Test]
        public void SceneSetBuild_SetsSceneList()
        {
            var originalScenes = EditorBuildSettings.scenes.ToArray();
            var scenePath = $"{TestFolder}/BuildTest.unity";
            SceneManagementTools.CreateScene(scenePath);
            var json = $"[{{\"path\":\"{scenePath}\",\"enabled\":true}}]";
            var result = SceneManagementTools.SetBuildScenes(json);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.AreEqual(1, EditorBuildSettings.scenes.Length);
            Assert.AreEqual(scenePath, EditorBuildSettings.scenes[0].path);
            EditorBuildSettings.scenes = originalScenes;
        }
    }
}
