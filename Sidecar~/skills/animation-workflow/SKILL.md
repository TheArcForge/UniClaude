---
name: animation-workflow
description: "Use when setting up animation in the Unity Editor using MCP tools — creating AnimatorControllers, configuring states and transitions, assigning clips, and editing clip import settings."
---

# Animation Workflow with MCP Tools

Efficient tool sequences for animation setup.

## GOAL: Create an Animator Controller from Scratch

**Approach:**
1. Create the controller with all parameters, states, and transitions in one call.
2. Assign clips to states.
3. Assign controller to GameObject.

**Tool sequence:**
- `animation_create_controller` — single batch call creates the full state machine:
  - Parameters (bool, float, int, trigger)
  - States with motion clip references
  - Transitions with conditions
- `animation_assign_clip` — assign AnimationClip assets to states (if not set during creation).
- `animation_assign_controller` — apply controller to a GameObject (adds Animator if missing).

**Common mistakes:**
- Creating empty controller then adding states one by one (inefficient — use the batch call).
- Forgetting to set a default state (first state added is default).
- Not creating parameters before referencing them in transition conditions.

## GOAL: Edit an Existing Controller

**Approach:**
1. Inspect current state.
2. Batch add/remove operations.

**Tool sequence:**
- `animation_get_controller` — inspect parameters, states, transitions, layers.
- `animation_edit_controller` — batch modify: add/remove parameters, states, transitions.

**Common mistakes:**
- Removing a state that other transitions reference (leaves dangling transitions).
- Adding duplicate parameter names (will error).

## GOAL: Configure Animation Clip Import Settings

**When to use:** Setting loop behavior, frame ranges on FBX/model animation clips.

**Tool sequence:**
- `asset_get_import_settings` — read current clip settings on the model.
- `asset_set_clip_import_settings` — set loopTime, loopPose, start/end frames per clip.

**Common mistakes:**
- Setting loop on the wrong clip name (FBX can have multiple clips — check names first).
- Forgetting that import setting changes require reimport (the tool handles this automatically).

## GOAL: Set Up a Character with Animations

**Full workflow example:**

1. `animation_create_controller` — create controller:
   - Parameters: Speed (float), IsJumping (bool), Attack (trigger)
   - States: Idle (default), Walk, Run, Jump, Attack
   - Transitions: Idle→Walk (Speed > 0.1), Walk→Run (Speed > 0.5), Any→Jump (IsJumping), Any→Attack (Attack trigger)

2. `animation_assign_clip` — assign clips:
   - Idle state ← "Assets/Animations/Idle.anim"
   - Walk state ← "Assets/Animations/Walk.anim"
   - etc.

3. `asset_set_clip_import_settings` — configure clips:
   - Idle: loopTime = true
   - Walk: loopTime = true
   - Attack: loopTime = false

4. `animation_assign_controller` — assign to character GameObject.

5. `component_set_property` on Animator — set applyRootMotion if needed.

## GOAL: Inspect Animation State

**Tool sequence:**
- `animation_get_controller` — returns full state machine: all parameters (name, type, default value), all states (name, motion clip, speed), all transitions (source, destination, conditions, duration), all layers.

**Use for:** Understanding existing animation before modifying, verifying setup is correct after changes.

## Related Skills

- uniclaude:unity-architect — when to use animation (state machine for character vs simple playback)
- uniclaude:component-design — Animator component lifecycle, event communication from animation
