# Component Design — Deep Dive

Supplements the DECISION: Component Design block in the parent SKILL.md.

## Communication Between Components

**Direct reference (serialized field):**
- Use when: components are on the same GameObject or known hierarchy.
- Set up with reference_set or component_set_property in the editor.
- Simplest, fastest, but creates tight coupling.

**Events (C# delegates/Action):**
- Use when: one-to-many notification, sender doesn't need to know receivers.
- Pattern: public event Action<DamageInfo> OnDamageTaken.
- Subscribe in OnEnable, unsubscribe in OnDisable — always.

**Interface-based (GetComponent<IInterface>()):**
- Use when: you need polymorphism without knowing the concrete type.
- Pattern: if (TryGetComponent<IDamageable>(out var target)) target.TakeDamage(10);
- Cache the result if calling frequently.

**ScriptableObject event channels:**
- Use when: decoupled systems that shouldn't reference each other (UI listens to GameState).
- Pattern: SO asset with Raise()/Register()/Unregister() methods.
- No scene references needed — both systems reference the same SO asset.

## Single Responsibility Signals

Split a MonoBehaviour when:
- It has fields for two unrelated purposes (movement speed AND health max in one script).
- You want to reuse part of its behavior on a different object.
- It exceeds 150 lines and has distinct sections.
- Two developers would need to edit it for unrelated reasons.

Do NOT split when:
- The logic is inherently sequential (state machine with 3 states in 80 lines).
- Splitting would require constant cross-component calls for trivial data.
- The "separate concerns" are actually one concern seen from two angles.

## Execution Order

- Awake: initialize self (get own components, set defaults). No cross-object calls.
- OnEnable: subscribe to events, register with managers.
- Start: cross-object initialization (find other objects, validate references).
- OnDisable: unsubscribe from events, unregister from managers.
- OnDestroy: cleanup resources (dispose native containers, release assets).

Never call GetComponent or Find in Update/FixedUpdate/LateUpdate.
