# Data Modeling — Deep Dive

Supplements the DECISION: Data Architecture block in the parent SKILL.md.

## ScriptableObject Patterns

**Data container (most common):**
- [CreateAssetMenu] attribute for easy creation.
- Read-only at runtime. Clone if you need mutable copies.
- Use asset_find to locate existing SOs: `t:WeaponConfig`.

**Runtime set:**
- SO that holds a List<T> of active objects.
- Objects register in OnEnable, unregister in OnDisable.
- Use for: "give me all active enemies" without FindObjectsOfType.

**Event channel:**
- SO with Raise() and listeners list.
- Decouples publisher from subscriber — no scene references.
- Use for: game events (PlayerDied, ScoreChanged, LevelComplete).

## Serialization Decisions

- Inspector data → [SerializeField] private fields. Always private, never public.
- Save data → JSON or binary. Use a dedicated SaveData class, not MonoBehaviour fields.
- Network data → structs with INetworkSerializable (Netcode) or equivalent.
- Config data → ScriptableObject assets. Version them with a schemaVersion field.

## When Data Lives Where

| Data type | Where it lives | Example |
|-----------|---------------|---------|
| Designer-tuned constants | ScriptableObject asset | Weapon damage, enemy speed |
| Per-instance runtime state | MonoBehaviour field | Current health, cooldown timer |
| Global game state | Static class or singleton SO | Score, level index |
| Player progression | Serialized file (JSON) | Unlocks, inventory, settings |
| Temporary frame data | Local variable or struct | Raycast results, input vector |

## Anti-Patterns

- Public fields for Inspector exposure. Use [SerializeField] private.
- Storing runtime state in ScriptableObjects (shared across instances, persists in editor).
- Using PlayerPrefs for complex data (limited to string/int/float, no structure).
- Hardcoded magic numbers in scripts. Put them in a config SO.
