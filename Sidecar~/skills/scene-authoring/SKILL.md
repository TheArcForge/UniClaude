---
name: scene-authoring
description: "Use when building scenes in the Unity Editor using MCP tools — creating GameObjects, setting up hierarchies, configuring components, applying materials, and lighting."
---

# Scene Authoring with MCP Tools

Efficient tool sequences for building Unity scenes. Always prefer batch operations over individual calls.

## GOAL: Build a Scene from Scratch

**Approach:**
1. Create the scene asset.
2. Set up hierarchy structure (parent objects first).
3. Batch-create child objects with components.
4. Configure properties and references.
5. Save the scene.

**Tool sequence:**
- `scene_create` — create and open the new scene.
- `scene_setup` — batch create multiple GameObjects with components, properties, and hierarchy in one call. This is the most efficient tool for initial scene population.
- `component_set_properties` — batch set remaining properties across multiple objects.
- `reference_set` — wire up cross-object references.
- `material_assign` — apply materials to renderers.
- `scene_save` — save the scene.

**Common mistakes:**
- Creating objects one-by-one with scene_create_gameobject when scene_setup can batch them.
- Forgetting to save the scene after modifications.
- Setting properties before all objects exist (references will fail if target doesn't exist yet).

## GOAL: Set Up a Level with Primitives

**Approach:**
1. Create floor/walls with primitives.
2. Add physics colliders (auto-added with primitives).
3. Position and scale using component_set_properties.
4. Apply materials.

**Tool sequence:**
- `scene_create_primitive` — create Plane for floor (scale: 10,1,10).
- `scene_create_primitive` — create Cubes for walls (position + scale per wall).
- `material_create` — create materials for surfaces.
- `material_assign` — apply to renderers.
- `component_set_properties` — batch adjust transforms if needed.

**Common mistakes:**
- Forgetting that primitives come with colliders by default (no need to add separately).
- Not parenting walls under a "Walls" empty for organization.

## GOAL: Set Up Lighting

**Approach:**
1. Create light GameObjects.
2. Configure light component properties.
3. Set environment lighting in project settings.

**Tool sequence:**
- `scene_create_gameobject` — create empty, name "Directional Light".
- `component_add` — add Light component.
- `component_set_properties` — set type (Directional), color, intensity, shadows.
- For point/spot lights: `scene_create_gameobject` + `component_add` + position via transform.

**Common mistakes:**
- Multiple directional lights (expensive, usually wrong).
- Not setting shadow type (results in no shadows).

## GOAL: Add Physics to Existing Objects

**Approach:**
1. Identify objects that need physics.
2. Add Rigidbody + appropriate colliders.
3. Configure physics properties.

**Tool sequence:**
- `component_find` — find objects by existing component if needed.
- `component_add` — add Rigidbody to each.
- `component_add` — add colliders if not already present.
- `component_set_properties` — set mass, drag, constraints, collision detection mode.

**Common mistakes:**
- Adding MeshCollider to moving objects (use primitive colliders instead).
- Forgetting to set isKinematic on objects that should be moved by code, not physics.
- Not setting collision detection to Continuous for fast-moving objects.

## GOAL: Organize Scene Hierarchy

**Approach:**
1. Create empty parent objects for grouping.
2. Reparent existing objects under them.
3. Name consistently.

**Tool sequence:**
- `scene_create_gameobject` — create "--- Environment ---", "--- Gameplay ---", "--- UI ---" empties.
- `scene_reparent_gameobject` — move objects under appropriate parents.
- `scene_rename_gameobject` — fix naming inconsistencies.

**Naming convention:**
- Top-level groups: "--- GroupName ---" (dashes make them stand out).
- Objects: PascalCase, descriptive. "PlayerSpawnPoint" not "Spawn1".
- Instances: Name + number for multiples. "Wall_01", "Wall_02".

## GOAL: Set Up a Camera

**Approach:**
1. Create camera object (or find existing Main Camera).
2. Configure projection, FOV, clipping planes.
3. Position in scene.

**Tool sequence:**
- `scene_get_hierarchy` — check if Main Camera exists.
- If not: `scene_create_gameobject` + `component_add` (Camera) + tag as MainCamera.
- `component_set_properties` — set fieldOfView, nearClipPlane, farClipPlane, clearFlags.
- `component_set_property` on Transform — set position and rotation.

## Related Skills

- uniclaude:unity-architect — architecture decisions that inform scene structure
- uniclaude:scene-architecture — additive loading, transitions, persistent managers
