using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        /// <summary>
        /// Inspects an AnimatorController's full state machine: parameters, states, transitions per layer.
        /// </summary>
        [MCPTool("animation_get_controller", "Inspect an AnimatorController: parameters, states, transitions, layers")]
        public static MCPToolResult GetController(
            [MCPToolParam("AnimatorController asset path", required: true)] string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return MCPToolResult.Error($"AnimatorController not found at '{path}'.");

            var parameters = new List<object>();
            foreach (var p in controller.parameters)
            {
                object defaultValue = p.type switch
                {
                    AnimatorControllerParameterType.Float => (object)p.defaultFloat,
                    AnimatorControllerParameterType.Int => p.defaultInt,
                    AnimatorControllerParameterType.Bool => p.defaultBool,
                    _ => null
                };
                parameters.Add(new { name = p.name, type = p.type.ToString(), defaultValue });
            }

            var layers = new List<object>();
            for (var i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];
                var sm = layer.stateMachine;
                var defaultState = sm.defaultState;

                var states = new List<object>();
                foreach (var cs in sm.states)
                {
                    var clipPath = cs.state.motion != null
                        ? AssetDatabase.GetAssetPath(cs.state.motion)
                        : null;
                    states.Add(new
                    {
                        name = cs.state.name,
                        clipPath,
                        speed = cs.state.speed,
                        isDefault = cs.state == defaultState
                    });
                }

                var transitions = new List<object>();
                foreach (var cs in sm.states)
                {
                    foreach (var t in cs.state.transitions)
                    {
                        transitions.Add(FormatTransition(cs.state.name, t));
                    }
                }
                foreach (var t in sm.anyStateTransitions)
                {
                    transitions.Add(FormatTransition("AnyState", t));
                }

                layers.Add(new
                {
                    name = layer.name,
                    weight = layer.defaultWeight,
                    blendingMode = layer.blendingMode.ToString(),
                    states,
                    transitions
                });
            }

            return MCPToolResult.Success(new { path, parameters, layers });
        }

        /// <summary>
        /// Creates a new AnimatorController with optional parameters, states, and transitions.
        /// Supports batch setup of an entire state machine in a single call.
        /// </summary>
        [MCPTool("animation_create_controller",
            "Create an AnimatorController with optional parameters, states, and transitions in one call")]
        public static MCPToolResult CreateController(
            [MCPToolParam("Asset path ending in .controller", required: true)] string path,
            [MCPToolParam("JSON array: [{\"name\": \"Speed\", \"type\": \"Float\", \"default\": 0}]")] string parameters,
            [MCPToolParam("JSON array: [{\"name\": \"Idle\", \"clip\": \"Assets/.../Clip.fbx\", \"isDefault\": true}]")] string states,
            [MCPToolParam("JSON array: [{\"from\": \"AnyState\", \"to\": \"Idle\", \"hasExitTime\": false, \"duration\": 0.1, \"conditions\": [...]}]")] string transitions)
        {
            if (!path.EndsWith(".controller"))
                return MCPToolResult.Error($"Path must end with '.controller'. Got: '{path}'");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                return MCPToolResult.Error(
                    $"AnimatorController already exists at '{path}'. Use animation_edit_controller to modify it.");

            var dir = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                CreateFolderRecursive(dir);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            Undo.RegisterCreatedObjectUndo(controller, "MCP Create AnimatorController");

            var errors = new List<string>();

            if (!string.IsNullOrEmpty(parameters))
            {
                try
                {
                    var paramDefs = JsonConvert.DeserializeObject<ParamDef[]>(parameters);
                    foreach (var p in paramDefs)
                        AddParameter(controller, p, errors);
                }
                catch (JsonException ex)
                {
                    errors.Add($"Invalid parameters JSON: {ex.Message}");
                }
            }

            var sm = controller.layers[0].stateMachine;
            var stateMap = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);

            // Remove the default empty state that Unity creates
            if (sm.states.Length == 1 && sm.states[0].state.name == "New State")
            {
                sm.RemoveState(sm.states[0].state);
            }

            if (!string.IsNullOrEmpty(states))
            {
                try
                {
                    var stateDefs = JsonConvert.DeserializeObject<StateDef[]>(states);
                    var isFirstDefault = true;
                    foreach (var s in stateDefs)
                    {
                        var state = sm.AddState(s.Name);
                        stateMap[s.Name] = state;

                        if (!string.IsNullOrEmpty(s.Clip))
                        {
                            var clip = ResolveClipFromPath(s.Clip, out var clipError);
                            if (clip != null)
                                state.motion = clip;
                            else
                                errors.Add(clipError);
                        }

                        if (s.IsDefault || (isFirstDefault && !HasExplicitDefault(stateDefs)))
                        {
                            sm.defaultState = state;
                            isFirstDefault = false;
                        }
                        else
                        {
                            isFirstDefault = false;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    errors.Add($"Invalid states JSON: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(transitions))
            {
                try
                {
                    var transDefs = JsonConvert.DeserializeObject<TransitionDef[]>(transitions);
                    foreach (var t in transDefs)
                        AddTransition(sm, stateMap, controller, t, errors);
                }
                catch (JsonException ex)
                {
                    errors.Add($"Invalid transitions JSON: {ex.Message}");
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var stateNames = new List<string>();
            foreach (var cs in sm.states)
                stateNames.Add(cs.state.name);

            return MCPToolResult.Success(new
            {
                path,
                parameterCount = controller.parameters.Length,
                stateCount = sm.states.Length,
                transitionCount = sm.anyStateTransitions.Length + sm.states.Sum(s => s.state.transitions.Length),
                stateNames,
                errors
            });
        }

        /// <summary>
        /// Batch modifies an existing AnimatorController: add/remove parameters, states, and transitions.
        /// Removals execute before additions so replacements work in a single call.
        /// </summary>
        [MCPTool("animation_edit_controller",
            "Batch modify an existing AnimatorController: add/remove parameters, states, transitions")]
        public static MCPToolResult EditController(
            [MCPToolParam("AnimatorController asset path", required: true)] string path,
            [MCPToolParam("JSON array of parameters to add")] string addParameters,
            [MCPToolParam("JSON array of parameter names to remove")] string removeParameters,
            [MCPToolParam("JSON array of states to add")] string addStates,
            [MCPToolParam("JSON array of state names to remove")] string removeStates,
            [MCPToolParam("JSON array of transitions to add")] string addTransitions,
            [MCPToolParam("JSON array of {from, to} to remove")] string removeTransitions)
        {
            var hasAnyOp = !string.IsNullOrEmpty(addParameters) || !string.IsNullOrEmpty(removeParameters)
                || !string.IsNullOrEmpty(addStates) || !string.IsNullOrEmpty(removeStates)
                || !string.IsNullOrEmpty(addTransitions) || !string.IsNullOrEmpty(removeTransitions);

            if (!hasAnyOp)
                return MCPToolResult.Error("At least one add/remove parameter must be provided.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return MCPToolResult.Error($"AnimatorController not found at '{path}'.");

            Undo.IncrementCurrentGroup();
            Undo.RecordObject(controller, "MCP Edit AnimatorController");

            var sm = controller.layers[0].stateMachine;
            var errors = new List<string>();
            var added = new List<string>();
            var removed = new List<string>();

            // ── Removals first ──

            if (!string.IsNullOrEmpty(removeParameters))
            {
                try
                {
                    var names = JsonConvert.DeserializeObject<string[]>(removeParameters);
                    foreach (var name in names)
                    {
                        var idx = Array.FindIndex(controller.parameters, p => p.name == name);
                        if (idx >= 0)
                        {
                            controller.RemoveParameter(idx);
                            removed.Add($"parameter:{name}");
                        }
                        else
                            errors.Add($"Parameter '{name}' not found (skip).");
                    }
                }
                catch (JsonException ex) { errors.Add($"Invalid removeParameters JSON: {ex.Message}"); }
            }

            if (!string.IsNullOrEmpty(removeStates))
            {
                try
                {
                    var names = JsonConvert.DeserializeObject<string[]>(removeStates);
                    foreach (var name in names)
                    {
                        var found = false;
                        foreach (var cs in sm.states)
                        {
                            if (cs.state.name == name)
                            {
                                sm.RemoveState(cs.state);
                                removed.Add($"state:{name}");
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                            errors.Add($"State '{name}' not found (skip).");
                    }
                }
                catch (JsonException ex) { errors.Add($"Invalid removeStates JSON: {ex.Message}"); }
            }

            if (!string.IsNullOrEmpty(removeTransitions))
            {
                try
                {
                    var defs = JsonConvert.DeserializeObject<TransitionRemoveDef[]>(removeTransitions);
                    foreach (var def in defs)
                    {
                        var removedCount = RemoveTransitions(sm, def.From, def.To);
                        if (removedCount > 0)
                            removed.Add($"transition:{def.From}->{def.To} (x{removedCount})");
                        else
                            errors.Add($"No transition from '{def.From}' to '{def.To}' found (skip).");
                    }
                }
                catch (JsonException ex) { errors.Add($"Invalid removeTransitions JSON: {ex.Message}"); }
            }

            // ── Additions ──

            if (!string.IsNullOrEmpty(addParameters))
            {
                try
                {
                    var paramDefs = JsonConvert.DeserializeObject<ParamDef[]>(addParameters);
                    foreach (var p in paramDefs)
                    {
                        AddParameter(controller, p, errors);
                        added.Add($"parameter:{p.Name}");
                    }
                }
                catch (JsonException ex) { errors.Add($"Invalid addParameters JSON: {ex.Message}"); }
            }

            var stateMap = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);
            foreach (var cs in sm.states)
                stateMap[cs.state.name] = cs.state;

            if (!string.IsNullOrEmpty(addStates))
            {
                try
                {
                    var stateDefs = JsonConvert.DeserializeObject<StateDef[]>(addStates);
                    foreach (var s in stateDefs)
                    {
                        if (stateMap.ContainsKey(s.Name))
                        {
                            errors.Add($"State '{s.Name}' already exists.");
                            continue;
                        }
                        var state = sm.AddState(s.Name);
                        stateMap[s.Name] = state;

                        if (!string.IsNullOrEmpty(s.Clip))
                        {
                            var clip = ResolveClipFromPath(s.Clip, out var clipError);
                            if (clip != null) state.motion = clip;
                            else errors.Add(clipError);
                        }

                        if (s.IsDefault) sm.defaultState = state;
                        added.Add($"state:{s.Name}");
                    }
                }
                catch (JsonException ex) { errors.Add($"Invalid addStates JSON: {ex.Message}"); }
            }

            if (!string.IsNullOrEmpty(addTransitions))
            {
                try
                {
                    var transDefs = JsonConvert.DeserializeObject<TransitionDef[]>(addTransitions);
                    foreach (var t in transDefs)
                    {
                        AddTransition(sm, stateMap, controller, t, errors);
                        added.Add($"transition:{t.From}->{t.To}");
                    }
                }
                catch (JsonException ex) { errors.Add($"Invalid addTransitions JSON: {ex.Message}"); }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Undo.SetCurrentGroupName($"MCP Edit Controller: +{added.Count} -{removed.Count}");

            return MCPToolResult.Success(new { path, added, removed, errors });
        }

        // ── Helpers ──

        static MCPToolResult GameObjectNotFoundError(string path)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(r => r.name).ToArray();
            return MCPToolResult.Error(
                $"GameObject not found: '{path}'. Root objects in scene: {string.Join(", ", roots)}");
        }

        static object FormatTransition(string source, AnimatorStateTransition t)
        {
            var conditions = new List<object>();
            foreach (var c in t.conditions)
            {
                conditions.Add(new
                {
                    parameter = c.parameter,
                    mode = c.mode.ToString(),
                    threshold = c.threshold
                });
            }
            return new
            {
                source,
                destination = t.destinationState?.name ?? "(exit)",
                hasExitTime = t.hasExitTime,
                duration = t.duration,
                conditions
            };
        }

        /// <summary>
        /// Resolves an asset path to an AnimationClip. If the path points to an FBX,
        /// extracts the first non-preview AnimationClip sub-asset.
        /// </summary>
        internal static AnimationClip ResolveClipFromPath(string clipPath, out string error)
        {
            error = null;

            var directClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (directClip != null)
                return directClip;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(clipPath);
            if (allAssets == null || allAssets.Length == 0)
            {
                error = $"Asset not found at '{clipPath}'.";
                return null;
            }

            AnimationClip firstClip = null;
            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    if (firstClip == null)
                        firstClip = clip;
                }
            }

            if (firstClip == null)
            {
                error = $"No AnimationClip found in '{clipPath}'. " +
                        "If this is an FBX, ensure the rig type is set to Humanoid or Generic.";
                return null;
            }

            return firstClip;
        }

        static void AddParameter(AnimatorController controller, ParamDef p, List<string> errors)
        {
            if (!Enum.TryParse<AnimatorControllerParameterType>(p.Type, true, out var paramType))
            {
                errors.Add($"Invalid parameter type '{p.Type}'. Valid: Int, Float, Bool, Trigger");
                return;
            }
            controller.AddParameter(p.Name, paramType);

            if (p.Default != null)
            {
                var param = controller.parameters[controller.parameters.Length - 1];
                switch (paramType)
                {
                    case AnimatorControllerParameterType.Float:
                        param.defaultFloat = Convert.ToSingle(p.Default);
                        break;
                    case AnimatorControllerParameterType.Int:
                        param.defaultInt = Convert.ToInt32(p.Default);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        param.defaultBool = Convert.ToBoolean(p.Default);
                        break;
                }
                controller.parameters[controller.parameters.Length - 1] = param;
            }
        }

        static void AddTransition(AnimatorStateMachine sm, Dictionary<string, AnimatorState> stateMap,
            AnimatorController controller, TransitionDef t, List<string> errors)
        {
            if (!stateMap.TryGetValue(t.To, out var destState))
            {
                // Try finding in the state machine directly
                foreach (var cs in sm.states)
                {
                    if (string.Equals(cs.state.name, t.To, StringComparison.OrdinalIgnoreCase))
                    {
                        destState = cs.state;
                        break;
                    }
                }
                if (destState == null)
                {
                    var available = sm.states.Select(s => s.state.name).ToArray();
                    errors.Add($"Transition destination '{t.To}' not found. Available states: {string.Join(", ", available)}");
                    return;
                }
            }

            AnimatorStateTransition transition;

            if (string.Equals(t.From, "AnyState", StringComparison.OrdinalIgnoreCase))
            {
                transition = sm.AddAnyStateTransition(destState);
            }
            else
            {
                if (!stateMap.TryGetValue(t.From, out var srcState))
                {
                    foreach (var cs in sm.states)
                    {
                        if (string.Equals(cs.state.name, t.From, StringComparison.OrdinalIgnoreCase))
                        {
                            srcState = cs.state;
                            break;
                        }
                    }
                    if (srcState == null)
                    {
                        var available = sm.states.Select(s => s.state.name).ToArray();
                        errors.Add($"Transition source '{t.From}' not found. Available states: {string.Join(", ", available)}");
                        return;
                    }
                }
                transition = srcState.AddTransition(destState);
            }

            transition.hasExitTime = t.HasExitTime ?? true;
            transition.duration = t.Duration ?? 0.25f;

            if (t.Conditions != null)
            {
                foreach (var c in t.Conditions)
                {
                    if (!Enum.TryParse<AnimatorConditionMode>(c.Mode, true, out var mode))
                    {
                        errors.Add($"Invalid condition mode '{c.Mode}'. Valid: If, IfNot, Greater, Less, Equals, NotEqual");
                        continue;
                    }

                    var paramExists = false;
                    foreach (var p in controller.parameters)
                    {
                        if (p.name == c.Parameter)
                        {
                            paramExists = true;
                            break;
                        }
                    }
                    if (!paramExists)
                    {
                        var paramNames = controller.parameters.Select(p => p.name).ToArray();
                        errors.Add($"Parameter '{c.Parameter}' not found. Available: {string.Join(", ", paramNames)}");
                        continue;
                    }

                    transition.AddCondition(mode, c.Threshold, c.Parameter);
                }
            }
        }

        static bool HasExplicitDefault(StateDef[] stateDefs)
        {
            foreach (var s in stateDefs)
                if (s.IsDefault) return true;
            return false;
        }

        static void CreateFolderRecursive(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                CreateFolderRecursive(parent);
            AssetDatabase.CreateFolder(parent ?? "", System.IO.Path.GetFileName(folderPath));
        }

        static int RemoveTransitions(AnimatorStateMachine sm, string from, string to)
        {
            var count = 0;

            if (string.Equals(from, "AnyState", StringComparison.OrdinalIgnoreCase))
            {
                var toRemove = sm.anyStateTransitions
                    .Where(t => string.Equals(t.destinationState?.name, to, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var t in toRemove)
                {
                    sm.RemoveAnyStateTransition(t);
                    count++;
                }
            }
            else
            {
                foreach (var cs in sm.states)
                {
                    if (!string.Equals(cs.state.name, from, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var toRemove = cs.state.transitions
                        .Where(t => string.Equals(t.destinationState?.name, to, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    foreach (var t in toRemove)
                    {
                        cs.state.RemoveTransition(t);
                        count++;
                    }
                }
            }

            return count;
        }

        // ── Data Models ──

        class ParamDef
        {
            [JsonProperty("name")] public string Name;
            [JsonProperty("type")] public string Type;
            [JsonProperty("default")] public object Default;
        }

        class StateDef
        {
            [JsonProperty("name")] public string Name;
            [JsonProperty("clip")] public string Clip;
            [JsonProperty("isDefault")] public bool IsDefault;
        }

        class TransitionDef
        {
            [JsonProperty("from")] public string From;
            [JsonProperty("to")] public string To;
            [JsonProperty("hasExitTime")] public bool? HasExitTime;
            [JsonProperty("duration")] public float? Duration;
            [JsonProperty("conditions")] public ConditionDef[] Conditions;
        }

        class ConditionDef
        {
            [JsonProperty("parameter")] public string Parameter;
            [JsonProperty("mode")] public string Mode;
            [JsonProperty("threshold")] public float Threshold;
        }

        class TransitionRemoveDef
        {
            [JsonProperty("from")] public string From;
            [JsonProperty("to")] public string To;
        }
    }
}
