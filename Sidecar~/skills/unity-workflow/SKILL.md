---
name: unity-workflow
description: "Use when designing or building Unity features and superpowers:brainstorming is NOT available. Provides a Unity-specific research, design, implement, verify workflow for non-trivial tasks."
---

# Unity Development Workflow

A lightweight design-first process for Unity features. Follow these phases in order. Do not skip phases — even for tasks that seem simple.

If superpowers:brainstorming is available, prefer that for the design phase — it provides stronger process enforcement. This skill is the fallback when superpowers is not installed.

## Phase 1: Analyze

Before doing anything, understand the scope:

- What is the user building? Name the feature in one sentence.
- What existing systems does it touch? Check the scene hierarchy, existing scripts, prefabs.
- What are the unknowns? Identify anything ambiguous in the request.
- Does this need networking, persistence, or live ops integration?
- What is the performance profile? (spawned frequently? rendered every frame? loaded once?)

Ask clarifying questions until there is no ambiguity. Do not proceed with assumptions.

## Phase 2: Design

Present your architectural approach before building:

- State which pattern you will use and why (prefab vs variant, SO vs MonoBehaviour, pool vs instantiate).
- Identify the files you will create or modify.
- If multiple systems are involved, sketch the data flow between them.
- Call out tradeoffs explicitly — what you gain and what you give up.
- If the uniclaude:unity-architect skill is available, load it for detailed decision frameworks.

Get explicit user approval before proceeding to implementation.

## Phase 3: Implement

Execute the approved design:

- Use MCP tools for all scene, prefab, and asset authoring.
- Write C# scripts for runtime behavior only.
- State what you are doing at each step — do not work silently.
- Use batch operations where possible (scene_setup, component_set_properties, animation_create_controller).
- Wrap script modifications in BeginScriptEditing / EndScriptEditing.
- If the relevant workflow skill is available (uniclaude:scene-authoring, uniclaude:prefab-workflow, uniclaude:animation-workflow), load it for efficient tool sequences.

## Phase 4: Verify

After implementation, confirm the work is correct:

- If you modified code: review for Unity anti-patterns (GetComponent in Update, public fields, missing unsubscribes). Load uniclaude:unity-reviewer if available.
- Inspect the scene hierarchy to confirm the structure matches the design.
- Run project_run_tests if tests exist for the affected systems.
- Check project_get_console_log for errors or warnings.
- Report what was built and any remaining work the user needs to handle manually.

## Related Skills

- uniclaude:unity-architect — architecture decision frameworks
- uniclaude:scene-authoring — efficient scene building with MCP tools
- uniclaude:prefab-workflow — prefab creation and editing patterns
- uniclaude:animation-workflow — animation controller setup
- uniclaude:unity-reviewer — code review for Unity anti-patterns
