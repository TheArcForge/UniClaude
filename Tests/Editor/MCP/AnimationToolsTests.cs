using System.Linq;
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

        [Test]
        public void GetController_ReturnsParametersAndStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            AssetDatabase.SaveAssets();

            var result = AnimationTools.GetController(_controllerPath);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");
            StringAssert.Contains("Speed", result.Text);
            StringAssert.Contains("Idle", result.Text);
            StringAssert.Contains("Float", result.Text);
        }

        [Test]
        public void GetController_NotFound_ReturnsError()
        {
            var result = AnimationTools.GetController("Assets/NonExistent.controller");
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void ResolveClipFromPath_AnimClip_ReturnsClip()
        {
            var clip = AnimationTools.ResolveClipFromPath(_clipPath, out var error);
            Assert.IsNotNull(clip, $"Expected clip but got error: {error}");
            Assert.IsNull(error);
        }

        [Test]
        public void ResolveClipFromPath_NotFound_ReturnsNull()
        {
            var clip = AnimationTools.ResolveClipFromPath("Assets/NonExistent.anim", out var error);
            Assert.IsNull(clip);
            Assert.IsNotNull(error);
        }

        [Test]
        public void CreateController_EmptyController_CreatesFile()
        {
            var path = $"{TestFolder}/NewController.controller";
            var result = AnimationTools.CreateController(path, null, null, null);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.IsNotNull(controller);
        }

        [Test]
        public void CreateController_WithParametersAndStates_ConfiguresAll()
        {
            var path = $"{TestFolder}/FullController.controller";
            var parameters = "[{\"name\": \"Speed\", \"type\": \"Float\", \"default\": 0.0}]";
            var states = $"[{{\"name\": \"Idle\", \"clip\": \"{_clipPath}\", \"isDefault\": true}}, {{\"name\": \"Walk\"}}]";
            var transitions = "[{\"from\": \"AnyState\", \"to\": \"Idle\", \"hasExitTime\": false, \"duration\": 0.1, \"conditions\": [{\"parameter\": \"Speed\", \"mode\": \"Less\", \"threshold\": 0.1}]}]";

            var result = AnimationTools.CreateController(path, parameters, states, transitions);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.AreEqual(1, controller.parameters.Length);
            Assert.AreEqual("Speed", controller.parameters[0].name);

            var sm = controller.layers[0].stateMachine;
            Assert.AreEqual(2, sm.states.Length);
            Assert.AreEqual(1, sm.anyStateTransitions.Length);
        }

        [Test]
        public void CreateController_AlreadyExists_ReturnsError()
        {
            var result = AnimationTools.CreateController(_controllerPath, null, null, null);
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("already exists", result.Text);
        }

        [Test]
        public void CreateController_InvalidPath_ReturnsError()
        {
            var result = AnimationTools.CreateController($"{TestFolder}/Bad.txt", null, null, null);
            Assert.IsTrue(result.IsError);
            StringAssert.Contains(".controller", result.Text);
        }

        [Test]
        public void EditController_AddState_AddsToExisting()
        {
            var states = "[{\"name\": \"Walking\"}]";
            var result = AnimationTools.EditController(_controllerPath, null, null, states, null, null, null);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            var stateNames = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            CollectionAssert.Contains(stateNames, "Walking");
            CollectionAssert.Contains(stateNames, "Idle");
        }

        [Test]
        public void EditController_RemoveState_RemovesFromExisting()
        {
            var result = AnimationTools.EditController(_controllerPath, null, null, null, "[\"Idle\"]", null, null);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            var stateNames = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            CollectionAssert.DoesNotContain(stateNames, "Idle");
        }

        [Test]
        public void EditController_AddParameter_AddsToExisting()
        {
            var parameters = "[{\"name\": \"Health\", \"type\": \"Float\", \"default\": 100.0}]";
            var result = AnimationTools.EditController(_controllerPath, parameters, null, null, null, null, null);
            Assert.IsFalse(result.IsError, $"Expected success: {result.Text}");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(_controllerPath);
            Assert.AreEqual(1, controller.parameters.Length);
            Assert.AreEqual("Health", controller.parameters[0].name);
        }

        [Test]
        public void EditController_NotFound_ReturnsError()
        {
            var result = AnimationTools.EditController("Assets/NonExistent.controller", null, null, "[{\"name\": \"X\"}]", null, null, null);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void EditController_NoOps_ReturnsError()
        {
            var result = AnimationTools.EditController(_controllerPath, null, null, null, null, null, null);
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("at least one", result.Text.ToLower());
        }
    }
}
