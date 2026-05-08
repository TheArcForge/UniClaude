---
name: prefab-architecture
description: "Use when you need detailed decision frameworks for prefab vs variant vs ScriptableObject, nested prefab design, override strategies, and prefab editing workflows."
---

# Prefab Architecture — Full Decision Framework

Use this when unity-architect's condensed prefab section needs more depth.

## DECISION: Prefab vs Variant vs ScriptableObject Config

**When this applies:** Creating a family of related objects (weapons, enemies, pickups, vehicles).

**Options:**

1. **Separate prefabs** — each variant is an independent prefab asset.
   - Use when: variants differ in structure (different components, different child hierarchies).
   - Use when: fewer than 5 variants total.
   - Tradeoff: changes to shared structure must be replicated manually across all.

2. **Prefab variants** — base prefab + variants that override specific values.
   - Use when: variants share structure but differ in property values or additive children.
   - Use when: 3-15 variants needed.
   - Tradeoff: cannot remove components/children from base — only add or override.
   - Tradeoff: variant chains deeper than 2 levels are fragile (override conflicts).

3. **ScriptableObject config + single prefab** — one prefab, multiple SO data assets.
   - Use when: variants are data-only (different stats, sprites, names, costs).
   - Use when: 10+ variants needed (scalable).
   - Tradeoff: less editor preview (need to play to see configured variant).
   - Tradeoff: requires a "configurator" script on the prefab that reads the SO.

**Decision tree:**
```
Do variants differ in components or children?
├── YES → Separate prefabs (or variant if additive only)
└── NO → Data-only differences?
    ├── YES → How many variants?
    │   ├── < 10 → Prefab variants (visual editor preview)
    │   └── >= 10 → ScriptableObject config (scalable)
    └── MIXED → Base prefab variant + SO for data portions
```

**Questions to ask the user:**
- How many variants will exist now? In 6 months?
- Do designers need drag-and-drop visual preview per variant?
- Will new variants be added by non-programmers?

## DECISION: Nested Prefab Structure

**When this applies:** A prefab has children that are themselves reusable.

**Rule:** Nest when children are independently reusable. Flatten when they are internal implementation.

**Examples:**
- Character → Weapon (nested prefab) — weapon is reused across characters.
- Character → HealthBar UI (nested prefab) — health bar is reused on enemies too.
- Character → LeftArm → Shoulder → Elbow (NOT nested) — these are internal to the character rig.

**Depth limit:** Never deeper than Base → Nested. Two levels of nesting (Base → Nested → Nested) causes override resolution conflicts in Unity.

## DECISION: Override Strategy

**When this applies:** Deciding how to manage changes between base and variants.

**Rules:**
- Apply overrides immediately after intentional changes (prefab_apply_overrides).
- Keep overrides minimal — if a variant overrides more than 5 properties, consider a separate prefab.
- Document WHY a variant differs (in a component field or naming convention, e.g., "Enemy_Fast", "Enemy_Tank").

**MCP workflow:**
1. To create a variant: `prefab_create_variant` from base.
2. To edit a variant: `prefab_open_editing` → make changes → `prefab_save_editing`.
3. To propagate base changes: edit the base; variants auto-inherit unless overridden.
4. To check what's overridden: `prefab_get_contents` on the variant asset.

## Related Skills

- uniclaude:unity-architect — condensed prefab decisions
- uniclaude:prefab-workflow — step-by-step MCP tool sequences for prefab operations
- uniclaude:data-modeling — ScriptableObject patterns for config-based variants
