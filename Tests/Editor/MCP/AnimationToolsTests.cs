using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="AnimationTools"/> MCP tools.
    /// </summary>
    public class AnimationToolsTests
    {
        const string TestFolder = "Assets/UniClaudeTestTemp";
        string _controllerPath;
        string _clipPath;
        GameObject _tempGO;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "UniClaudeTestTemp");

            _controllerPath = $"{TestFolder}/TestController.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            stateMachine.AddState("Idle");
            AssetDatabase.SaveAssets();

            _clipPath = $"{TestFolder}/TestClip.anim";
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, _clipPath);
            AssetDatabase.SaveAssets();

            _tempGO = new GameObject("AnimTestGO");
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
        public void AssignController_AddsAnimatorAndSetsController()
        {
            var result = AnimationTools.AssignController("AnimTestGO", _controllerPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var animator = _tempGO.GetComponent<Animator>();
            Assert.IsNotNull(animator, "Animator component should have been added");
            Assert.IsNotNull(animator.runtimeAnimatorController, "Controller should be assigned");
        }

        [Test]
        public void AssignController_NotFound_ReturnsError()
        {
            var result = AnimationTools.AssignController("AnimTestGO", "Assets/NonExistent.controller");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void AssignController_GONotFound_ReturnsError()
        {
            var result = AnimationTools.AssignController("NonExistentGO999", _controllerPath);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void AssignClip_SetsMotionOnState()
        {
            var result = AnimationTools.AssignClip(_controllerPath, "Idle", _clipPath);
            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            var state = controller.layers[0].stateMachine.states[0].state;
            Assert.IsNotNull(state.motion, "Motion should be assigned");
        }

        [Test]
        public void AssignClip_StateNotFound_ReturnsError()
        {
            var result = AnimationTools.AssignClip(_controllerPath, "NonExistentState", _clipPath);
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
            StringAssert.Contains("Idle", result.Text);
        }

        [Test]
        public void AssignClip_ClipNotFound_ReturnsError()
        {
            var result = AnimationTools.AssignClip(_controllerPath, "Idle", "Assets/NonExistent.anim");
            Assert.IsTrue(result.IsError);
        }
    }
}
