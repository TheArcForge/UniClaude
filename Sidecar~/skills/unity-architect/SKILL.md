---
name: unity-architect
description: "Use when making Unity architecture decisions — system design, scope analysis, feature planning. Covers prefab strategy, scene structure, component design, data modeling, and performance considerations."
---

# Unity Architecture Decisions

Load this skill when designing a Unity feature or system. It contains condensed decision frameworks for the most common architectural choices. For deeper detail on any domain, reference files are available in references/.

## Before You Design

Ask these questions for every non-trivial feature:

1. **Scale:** How many instances? (1, tens, hundreds, thousands?)
2. **Lifetime:** Persistent across scenes? Created/destroyed at runtime? Pooled?
3. **Variance:** How do instances differ? (data only? behavior? visuals?)
4. **Ownership:** Who spawns it? Who configures it? Who destroys it?
5. **Dependencies:** What other systems does it talk to? (input, networking, UI, save system, audio?)
6. **Performance budget:** Is this in a hot path? Per-frame? Event-driven?

## DECISION: Object Representation

**When this applies:** You need reusable objects in the game (enemies, weapons, pickups, UI panels, level pieces).

**Options:**
1. **Prefab** — self-contained GameObject template with components and children.
   - Use when: objects share identical structure but may have different runtime state.
   - Tradeoff: each variant is a full copy; changes to shared structure require updating all.
2. **Prefab Variant** — inherits from a base prefab, overrides specific properties.
   - Use when: objects share structure but differ in specific property values or child additions.
   - Tradeoff: override tracking adds complexity; deep nesting (variant of variant) is fragile.
3. **ScriptableObject config + single prefab** — one prefab reads data from a SO asset.
   - Use when: objects are structurally identical but differ only in data (stats, costs, names, sprites).
   - Tradeoff: requires a script to apply config at runtime; less visual preview in editor.

**Questions to ask:**
- Do variants differ in structure (different components/children) or only data?
- How many variants will exist? (3-5 → prefab variants. 50+ → SO config.)
- Do designers need to preview each variant visually in the editor?

## DECISION: Spawning Strategy

**When this applies:** Objects are created at runtime (projectiles, enemies, effects, UI elements).

**Options:**
1. **Direct Instantiate** — `Instantiate(prefab)` when needed.
   - Use when: few spawns per second, objects live for a long time, simplicity matters.
   - Tradeoff: GC pressure from frequent create/destroy.
2. **Object Pool** — pre-allocate, deactivate instead of destroy, reuse.
   - Use when: frequent spawning (projectiles, particles, hit numbers), short-lived objects.
   - Tradeoff: pool management complexity, need to reset object state on reuse.
3. **Factory pattern** — abstraction layer that decides pooling vs instantiate internally.
   - Use when: spawning logic is complex (needs DI, needs configuration, multiple callers).
   - Tradeoff: indirection; only worth it if multiple systems spawn the same type.

**Questions to ask:**
- How many per second? (< 5/s → instantiate is fine. > 10/s → pool.)
- Is GC stuttering acceptable? (Mobile: no. PC: maybe.)
- Who requests the spawn? (Single caller → simple. Multiple → factory.)

## DECISION: Scene Architecture

**When this applies:** Structuring game scenes, loading, transitions.

**Options:**
1. **Single scene** — everything in one scene, swap content at runtime.
   - Use when: small game, jam prototype, no loading needed.
   - Tradeoff: no streaming, single point of failure, large scene file.
2. **Additive loading** — base scene (managers) + content scenes loaded additively.
   - Use when: levels, rooms, streaming world, persistent UI across levels.
   - Tradeoff: scene dependency management, need to track what's loaded.
3. **Scene-per-screen** — each major UI/game state is its own scene (Menu, Game, Results).
   - Use when: distinct game states with different asset needs, clean teardown between states.
   - Tradeoff: transition latency, need persistent data across scene loads.

**Questions to ask:**
- Are there persistent objects that survive scene transitions? (audio manager, player data)
- Does the game need streaming/progressive loading?
- How large are individual levels in terms of assets?

## DECISION: Component Design

**When this applies:** Designing MonoBehaviour structure for a GameObject.

**Options:**
1. **Single MonoBehaviour** — one component handles all behavior.
   - Use when: behavior is simple and cohesive (< 100 lines), no reuse needed.
   - Tradeoff: grows unwieldy fast; impossible to reuse parts independently.
2. **Composition (multiple focused components)** — each component handles one concern.
   - Use when: behaviors are independent or reusable (Health, Movement, Damage are separate).
   - Tradeoff: inter-component communication needs design (events, direct references, interfaces).
3. **Controller + data split** — one controller MonoBehaviour + ScriptableObject for config.
   - Use when: same behavior with different tuning per variant.
   - Tradeoff: two assets to manage per variant.

**Questions to ask:**
- Can any of this behavior be reused on a different object?
- Do designers need to tweak values without touching code?
- Will this grow beyond 150 lines? If yes, split now.

## DECISION: Data Architecture

**When this applies:** Storing and accessing game data (stats, config, progression, inventory).

**Options:**
1. **ScriptableObject assets** — data lives in .asset files, referenced by components.
   - Use when: designer-authored data (item stats, level configs, dialogue), read-only at runtime.
   - Tradeoff: not suitable for runtime-mutable state (unless you clone).
2. **MonoBehaviour with [SerializeField]** — data lives on the GameObject.
   - Use when: per-instance state that varies at runtime (current health, position, cooldown timer).
   - Tradeoff: not shareable across instances.
3. **Static/singleton config** — loaded once, available globally.
   - Use when: global constants, game settings, truly global state.
   - Tradeoff: tight coupling, hard to test, survives scene loads (intentional or not).
4. **Serialized JSON/binary** — runtime save/load to disk.
   - Use when: player progression, save games, user preferences.
   - Tradeoff: serialization complexity, versioning across updates.

**Questions to ask:**
- Is this data authored by designers or generated at runtime?
- Does it need to persist across sessions? (save data)
- Is it shared across all instances or per-instance?
- Does it need to be hot-reloadable in the editor?

## DECISION: Performance Strategy

**When this applies:** Any system that runs per-frame or handles many objects.

**Key cost model:**
- **CPU:** GetComponent calls, Find calls, allocations in Update, physics queries, string operations
- **GPU:** draw calls (batch!), overdraw, shader complexity, resolution, post-processing
- **RAM:** texture size, mesh complexity, audio clips, duplicated materials

**Options by concern:**

*Spawning:* Pool when > 10/s or on mobile. Instantiate otherwise.

*Rendering:* Static batching (non-moving objects), GPU instancing (many identical meshes), SRP batcher (different materials, same shader). Set objects static when they never move.

*Physics:* Simple colliders over mesh colliders. Reduce FixedUpdate frequency if possible. Layer-based collision matrix to skip irrelevant pairs.

*Materials:* Share materials to enable batching. Texture atlases for sprites. Avoid runtime material instantiation (creates unique materials that break batching).

*Lighting:* Bake everything that does not move. Mixed lighting for mostly-static scenes. Realtime only for fully dynamic scenes. Light probes for dynamic objects in baked scenes.

**Questions to ask:**
- What's the target platform? (Mobile budgets are 10x tighter.)
- What's the target framerate? (30 vs 60 vs 120 changes priorities.)
- Where is the bottleneck likely? (CPU-bound: logic/physics. GPU-bound: rendering. Memory: asset loading.)

## Cross-Cutting Concerns

Before finalizing any design, verify these:

- **Networking:** Does any of this state need to sync across clients? If yes, which object owns authority? What's the sync frequency?
- **Persistence:** Does any of this survive a session? If yes, what format? What happens on version update?
- **Live ops:** Will this be configurable server-side? A/B tested? Feature-flagged?
- **Undo:** Are you using MCP tools? They integrate with Unity's undo system. Scripts that modify the scene should call Undo.RecordObject.
- **Testing:** Can you verify this works? Which tools would you use to inspect the result?

## After Designing

Once the user approves the architecture:
1. Load the relevant workflow skill (uniclaude:scene-authoring, uniclaude:prefab-workflow, uniclaude:animation-workflow) for efficient tool sequences.
2. Implement using MCP tools for authoring, C# for runtime behavior.
3. After implementation, load uniclaude:unity-reviewer to check for anti-patterns.

## Related Skills (Deep-Dives)

For detailed decision trees beyond this summary, see references/:
- references/prefab-decisions.md — nested variants, override strategies, prefab editing workflows
- references/scene-decisions.md — additive loading patterns, DontDestroyOnLoad, scene dependencies
- references/component-decisions.md — event communication patterns, interface-based composition, execution order
- references/data-decisions.md — SO runtime sets, SO event channels, serialization strategies
- references/performance-decisions.md — profiling approach, per-platform budgets, optimization priority order

Also available as standalone skills for on-demand loading:
- uniclaude:prefab-architecture
- uniclaude:scene-architecture
- uniclaude:component-design
- uniclaude:data-modeling
- uniclaude:unity-performance
