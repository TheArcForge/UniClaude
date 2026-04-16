using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for assigning AnimatorControllers and AnimationClips to GameObjects and states.
    /// </summary>
    public static class AnimationTools
    {
        /// <summary>
        /// Assigns an AnimatorController asset to a GameObject's Animator component.
        /// Adds an Animator component if one does not exist.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the GameObject.</param>
        /// <param name="controllerPath">Asset path of the AnimatorController.</param>
        /// <returns>Confirmation of assignment, or a contextual error.</returns>
        [MCPTool("animation_assign_controller", "Assign an AnimatorController to a GameObject (adds Animator if needed)")]
        public static MCPToolResult AssignController(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string gameObjectPath,
            [MCPToolParam("AnimatorController asset path (e.g. 'Assets/Animations/Player.controller')", required: true)] string controllerPath)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
                return MCPToolResult.Error(
                    $"AnimatorController not found at '{controllerPath}'. " +
                    "Ensure the path ends with .controller and is under the Assets folder.");

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(go);

            Undo.RecordObject(animator, "MCP Assign AnimatorController");
            var so = new SerializedObject(animator);
            var prop = so.FindProperty("m_Controller");
            prop.objectReferenceValue = controller;
            so.ApplyModifiedProperties();

            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                controller = controllerPath,
                addedAnimator = go.GetComponent<Animator>() == animator
            });
        }

        /// <summary>
        /// Assigns an AnimationClip to a named state in an AnimatorController.
        /// </summary>
        /// <param name="controllerPath">Asset path of the AnimatorController.</param>
        /// <param name="stateName">Name of the state to assign the clip to.</param>
        /// <param name="clipPath">Asset path of the AnimationClip.</param>
        /// <returns>Confirmation of assignment, or error with available state names.</returns>
        [MCPTool("animation_assign_clip", "Assign an AnimationClip to a named state in an AnimatorController")]
        public static MCPToolResult AssignClip(
            [MCPToolParam("AnimatorController asset path", required: true)] string controllerPath,
            [MCPToolParam("State name in the controller (e.g. 'Idle', 'Walk')", required: true)] string stateName,
            [MCPToolParam("AnimationClip asset path", required: true)] string clipPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return MCPToolResult.Error($"AnimatorController not found at '{controllerPath}'.");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                return MCPToolResult.Error($"AnimationClip not found at '{clipPath}'.");

            var stateNames = new List<string>();
            foreach (var layer in controller.layers)
            {
                foreach (var childState in layer.stateMachine.states)
                {
                    stateNames.Add(childState.state.name);
                    if (childState.state.name == stateName)
                    {
                        if (childState.state.motion is BlendTree)
                            return MCPToolResult.Error(
                                $"State '{stateName}' uses a BlendTree, not a single clip. " +
                                "BlendTree editing is not supported by this tool.");

                        Undo.RecordObject(childState.state, "MCP Assign AnimationClip");
                        childState.state.motion = clip;
                        EditorUtility.SetDirty(controller);
                        AssetDatabase.SaveAssets();

                        return MCPToolResult.Success(new
                        {
                            controller = controllerPath,
                            state = stateName,
                            clip = clipPath
                        });
                    }
                }
            }

            return MCPToolResult.Error(
                $"State '{stateName}' not found in controller. " +
                $"Available states: {string.Join(", ", stateNames)}");
        }

        // ── Helpers ──

        static MCPToolResult GameObjectNotFoundError(string path)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(r => r.name).ToArray();
            return MCPToolResult.Error(
                $"GameObject not found: '{path}'. Root objects in scene: {string.Join(", ", roots)}");
        }
    }
}
