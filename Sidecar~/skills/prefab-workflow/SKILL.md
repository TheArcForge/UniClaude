---
name: prefab-workflow
description: "Use when creating or editing prefabs in the Unity Editor using MCP tools — saving prefabs, editing prefab contents, creating variants, and applying overrides."
---

# Prefab Workflow with MCP Tools

Efficient tool sequences for prefab operations.

## GOAL: Create a Prefab from Scene Objects

**Approach:**
1. Build the object in the scene first (easier to iterate).
2. Once satisfied, save as prefab asset.
3. The scene instance becomes a prefab instance automatically.

**Tool sequence:**
- Build in scene using scene_create_gameobject, component_add, etc.
- `prefab_create` — save scene object as prefab at target path.

**Common mistakes:**
- Creating parent directories manually — prefab_create handles this.
- Forgetting to save the scene after creating the prefab (scene now has a prefab instance).

## GOAL: Edit a Prefab's Properties

**Two approaches depending on scope:**

**Single property change (atomic):**
- `prefab_edit_property` — load, edit one property, save. One tool call.

**Multi-step changes (session):**
- `prefab_open_editing` — opens prefab for editing, returns root path.
- Use component_add, component_set_property, reference_set, etc. on the opened prefab's hierarchy.
- `prefab_save_editing` — save and close.

**Common mistakes:**
- Using prefab_edit_property for many changes (inefficient — opens/saves per call).
- Forgetting prefab_save_editing (changes are lost).
- Trying to use scene tools on a prefab without opening it for editing first.

## GOAL: Create a Prefab Variant

**Approach:**
1. Create variant from base prefab.
2. Edit the variant's overriding properties.

**Tool sequence:**
- `prefab_create_variant` — specify base prefab path and new variant path.
- `prefab_edit_property` — override specific values on the variant.

**Common mistakes:**
- Trying to remove components from a variant (not supported — can only add or override).
- Creating deeply nested variants (Base → Variant → Variant) — fragile, avoid.

## GOAL: Instantiate Prefabs into the Scene

**Approach:**
1. Instantiate with optional parent.
2. Position/configure the instance.

**Tool sequence:**
- `prefab_instantiate` — create instance in scene, optionally parent it.
- `component_set_property` on Transform — set position, rotation, scale.
- `reference_set` — wire up any scene-specific references.

**Common mistakes:**
- Not parenting to an organizational empty (scene gets cluttered).
- Setting references to other prefab instances before they exist.

## GOAL: Apply Overrides Back to Prefab

**When to use:** You edited a prefab instance in the scene and want those changes in the prefab asset.

**Tool sequence:**
- `prefab_apply_overrides` — applies all instance overrides back to the source prefab.

**Common mistakes:**
- Applying when unintended changes are present on the instance.
- Use inspector_inspect on the instance first to verify what will be applied.

## GOAL: Inspect a Prefab Without Instantiating

**Tool sequence:**
- `prefab_get_contents` — returns the full hierarchy, components, and properties of a prefab asset without placing it in the scene.

**Use for:** Checking structure, verifying components, reading property values before deciding to instantiate or modify.

## Related Skills

- uniclaude:prefab-architecture — when to use prefabs vs variants vs SOs
- uniclaude:unity-architect — system-level prefab strategy
