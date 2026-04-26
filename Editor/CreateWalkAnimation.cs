using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ArcForge.UniClaude.Editor
{
    /// <summary>
    /// Editor utility to create and assign a WalkAnimation clip to an Animator Controller
    /// </summary>
    public static class CreateWalkAnimation
    {
        [MenuItem("ArcForge/Animation/Create Walk Animation Setup")]
        public static void CreateWalkAnimationSetup()
        {
            // Create Animation Clip
            AnimationClip walkClip = new AnimationClip();
            walkClip.name = "WalkAnimation";
            walkClip.legacy = false;
            
            // Create some basic animation curves for a simple walk cycle
            AnimationCurve xCurve = AnimationCurve.Linear(0f, 0f, 1f, 2f);
            AnimationCurve bobCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.25f, 0.1f),
                new Keyframe(0.5f, 0f),
                new Keyframe(0.75f, 0.1f),
                new Keyframe(1f, 0f)
            );
            
            // Add curves to the animation clip (assuming we're animating a Transform)
            walkClip.SetCurve("", typeof(Transform), "localPosition.x", xCurve);
            walkClip.SetCurve("", typeof(Transform), "localPosition.y", bobCurve);
            
            // Ensure the animations directory exists
            string animationsPath = "Assets/Animations";
            if (!AssetDatabase.IsValidFolder(animationsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }
            
            // Save the animation clip
            string clipPath = $"{animationsPath}/WalkAnimation.anim";
            AssetDatabase.CreateAsset(walkClip, clipPath);
            
            // Create Animator Controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath($"{animationsPath}/PlayerController.controller");
            
            // Add the walk animation as a state
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState walkState = rootStateMachine.AddState("Walk");
            walkState.motion = walkClip;
            
            // Set as default state
            rootStateMachine.defaultState = walkState;
            
            // Add a parameter to control walking
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            
            // Save assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("WalkAnimation clip and PlayerController created successfully!");
            Debug.Log($"Animation Clip: {clipPath}");
            Debug.Log($"Animator Controller: {animationsPath}/PlayerController.controller");
            
            // Select the created assets in the Project window
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        }
        
        [MenuItem("ArcForge/Animation/Assign Walk Animation to Selected")]
        public static void AssignWalkAnimationToSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("Please select a GameObject to assign the Animator to.");
                return;
            }
            
            // Get or add Animator component
            Animator animator = selected.GetComponent<Animator>();
            if (animator == null)
            {
                animator = selected.AddComponent<Animator>();
            }
            
            // Load the controller we created
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/PlayerController.controller");
            if (controller == null)
            {
                Debug.LogError("PlayerController not found. Please create the walk animation setup first.");
                return;
            }
            
            // Assign the controller
            animator.runtimeAnimatorController = controller;
            
            Debug.Log($"Assigned PlayerController to {selected.name}'s Animator component.");
        }
    }
}
