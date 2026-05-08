# Prefab Architecture — Deep Dive

Supplements the DECISION: Object Representation block in the parent SKILL.md.

## Nested Prefabs vs Flat

- Nest when children are reusable independently (a weapon prefab inside a character prefab).
- Keep flat when children are tightly coupled to the parent (internal bones, colliders, visual parts).
- Never nest deeper than 2 levels — override tracking becomes unreliable.

## Prefab Variant Chains

- Base → Variant is fine. Base → Variant → Variant is fragile.
- If you need a third level, reconsider: use SO config instead of variant overrides.
- Variants cannot remove components or children — only override property values or add new children.

## Override Management

- Apply overrides frequently. Unapplied overrides are invisible to other developers.
- Use prefab_apply_overrides after significant edits to commit changes back.
- When multiple team members edit the same prefab: one edits the base, others use variants.

## Editing Workflow with MCP Tools

- For single property changes: use prefab_edit_property (atomic load/edit/save).
- For multi-step changes: use prefab_open_editing → component/reference tools → prefab_save_editing.
- For creating from scene: build in scene first, then prefab_create to save as asset.
- For variants: prefab_create_variant from base, then prefab_edit_property to override values.

## When NOT to Use Prefabs

- Pure data (no visual, no components beyond a script) → ScriptableObject.
- Procedurally generated structure → build at runtime, don't save as prefab.
- Singleton managers → scene-based GameObject, not a prefab you instantiate.
