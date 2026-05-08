---
name: data-modeling
description: "Use when you need detailed decision frameworks for ScriptableObject patterns, runtime data management, serialization strategies, and data-driven architecture."
---

# Data Modeling — Full Decision Framework

Use this when unity-architect's condensed data section needs more depth.

## DECISION: ScriptableObject Pattern Selection

**When this applies:** Using SOs for game data.

**Options:**

1. **Data container** — simple read-only config.
   - Pattern: [CreateAssetMenu], public fields with stats/values.
   - Use: item definitions, enemy configs, level parameters.
   - Rule: never mutate at runtime (values persist in editor!). Clone first if needed.

2. **Runtime set** — tracks active objects without FindObjectsOfType.
   - Pattern: SO holds List<T>. Objects register in OnEnable, unregister in OnDisable.
   - Use: "all active enemies", "all collectibles in range", "all UI panels".
   - Benefit: O(1) removal, no per-frame scanning.

3. **Event channel** — decoupled pub/sub.
   - Pattern: SO with UnityEvent or Action. Publisher calls Raise(), subscribers listen.
   - Use: game events that cross system boundaries (PlayerDied, LevelComplete).
   - Benefit: no scene references needed between systems.

4. **Enum-like collection** — replaces C# enums with SO instances.
   - Pattern: each "enum value" is a SO asset. Systems reference the asset directly.
   - Use: damage types, item categories — when you need data attached to enum values.
   - Benefit: extensible without code changes; designers add new types.

## DECISION: Runtime State Management

**When this applies:** Data that changes during gameplay.

**Options:**

1. **MonoBehaviour fields** — state lives on the component.
   - Use when: per-instance, per-frame state (health, position, timers).
   - Access: other scripts hold a reference or use events.

2. **Dedicated state class (POCO)** — plain C# class holding state.
   - Use when: state needs to be serialized, copied, or passed around.
   - Pattern: [System.Serializable] class, owned by a MonoBehaviour.
   - Benefit: easy to serialize for save/load, easy to snapshot for networking.

3. **Static/global state** — accessible from anywhere.
   - Use ONLY for: truly global values (current score, game paused flag).
   - Implementation: static class with static properties or singleton SO.
   - Rule: minimize this. Most state should be instance-level.

## DECISION: Save System Architecture

**When this applies:** Player progression, settings, or game state that persists across sessions.

**Structure:**
```
SaveData (serializable class)
├── PlayerData (health, position, level)
├── InventoryData (list of item IDs + quantities)
├── SettingsData (volume, controls, display)
└── schemaVersion (int — for migration)
```

**Rules:**
- Serialize to JSON for debugging, binary for shipping.
- Include a version field for data migration between updates.
- Never serialize MonoBehaviour references — use IDs and look up on load.
- Save to Application.persistentDataPath — survives app updates.
- Backup before overwriting (copy current save to .bak).

## DECISION: Inspector Exposure

**When this applies:** Making data editable in the Unity Inspector.

**Rules:**
- Always [SerializeField] private — never public fields.
- Use [Header("Section")] to group related fields.
- Use [Tooltip("...")] for non-obvious fields.
- Use [Range(min, max)] for bounded numeric values.
- Use [TextArea] for multi-line strings.
- Complex data: use custom editors or [CreateAssetMenu] SOs.

## Related Skills

- uniclaude:unity-architect — condensed data decisions
- uniclaude:component-design — how components use and expose data
- uniclaude:prefab-architecture — SO config vs prefab variants
