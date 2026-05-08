---
name: component-design
description: "Use when you need detailed decision frameworks for MonoBehaviour composition, inter-component communication, single responsibility, and execution order."
---

# Component Design — Full Decision Framework

Use this when unity-architect's condensed component section needs more depth.

## DECISION: Splitting Responsibilities

**When this applies:** A script is growing or handles multiple concerns.

**Split signals (do split):**
- Script exceeds 150 lines with distinct sections.
- You want to reuse part of the behavior on another object.
- Two unrelated concerns share a file (movement code + health code).
- Adding a feature requires touching unrelated methods.

**Keep-together signals (do not split):**
- Logic is inherently sequential (< 100 lines).
- Splitting would require constant cross-calls for trivial data.
- The "separate concerns" are actually one concern (e.g., aiming and shooting are one "combat" concern).

**Naming convention:**
- [ObjectType][Concern]: PlayerMovement, PlayerHealth, PlayerInput, EnemyAI, EnemyVisuals.
- Never generic names: Manager, Controller, Handler (unless it actually manages a collection).

## DECISION: Inter-Component Communication

**When this applies:** Components on the same or different GameObjects need to interact.

**Options (ordered by coupling, lowest first):**

1. **ScriptableObject event channel** — fully decoupled, asset-based.
   - Use when: systems shouldn't know about each other (UI ← GameState → Audio).
   - Pattern: SO asset with Raise()/Register(). Both reference the same SO.
   - Tradeoff: indirection makes debugging harder; need SO assets per event type.

2. **C# events (Action/delegate)** — component exposes an event.
   - Use when: one-to-many within the same object or direct reference.
   - Pattern: public event Action<float> OnHealthChanged; invoked from the owner.
   - Rule: subscribe in OnEnable, unsubscribe in OnDisable. No exceptions.
   - Tradeoff: need reference to the publisher.

3. **Interface + GetComponent** — polymorphic access.
   - Use when: caller needs to interact with unknown implementations (IDamageable, IInteractable).
   - Pattern: if (collision.TryGetComponent<IDamageable>(out var target)) target.TakeDamage(10);
   - Tradeoff: GetComponent call cost (cache if used frequently).

4. **Direct serialized reference** — [SerializeField] to another component.
   - Use when: always the same specific component on a known object.
   - Set up with: reference_set MCP tool.
   - Tradeoff: tight coupling, breaks if hierarchy changes.

**Decision tree:**
```
Do the components know about each other?
├── NO (decoupled systems) → SO event channel
└── YES → One-to-many notification?
    ├── YES → C# event (Action<T>)
    └── NO → Need polymorphism?
        ├── YES → Interface + TryGetComponent
        └── NO → Direct [SerializeField] reference
```

## DECISION: Lifecycle Order

**Standard method ordering in a MonoBehaviour file:**

```
Fields (serialized, then private)
Awake()         → self-initialization only
OnEnable()      → subscribe to events, register
Start()         → cross-object initialization
FixedUpdate()   → physics logic
Update()        → per-frame logic
LateUpdate()    → follow/camera logic
OnDisable()     → unsubscribe, unregister
OnDestroy()     → dispose resources
```

**Rules:**
- Never call GetComponent/Find in Update — cache in Awake.
- Never access other objects in Awake — they may not be initialized yet. Use Start.
- Always pair OnEnable/OnDisable for event subscriptions.
- RequireComponent attribute if a component depends on another being present.

## Related Skills

- uniclaude:unity-architect — condensed component decisions
- uniclaude:data-modeling — ScriptableObject event channels and data patterns
