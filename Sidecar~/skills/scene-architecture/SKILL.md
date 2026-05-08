---
name: scene-architecture
description: "Use when you need detailed decision frameworks for scene structure, additive loading, scene management, transitions, and persistent managers."
---

# Scene Architecture — Full Decision Framework

Use this when unity-architect's condensed scene section needs more depth.

## DECISION: Scene Organization Pattern

**When this applies:** Deciding how to split a game into scenes.

**Options:**

1. **Monolithic (single scene)** — entire game in one scene.
   - Use when: game jam, prototype, very small game (< 50 GameObjects total).
   - Tradeoff: cannot stream content, large file, merge conflicts in teams.

2. **Scene-per-state** — Menu scene, Game scene, Results scene, etc.
   - Use when: distinct game states with clean boundaries.
   - Tradeoff: need persistent managers (audio, network) that survive loads.
   - Transition: LoadScene (replaces) or LoadSceneAsync for loading screens.

3. **Bootstrap + additive** — one persistent scene + content scenes loaded/unloaded.
   - Use when: persistent UI, managers, or audio across multiple levels.
   - Tradeoff: more complex loading logic, must manage active scene.
   - Pattern: Bootstrap loads first (scene 0 in build), then loads content additively.

4. **Streaming (additive + distance)** — world divided into chunks loaded by proximity.
   - Use when: open world, large levels, seamless transitions.
   - Tradeoff: complex trigger system, memory management, cross-scene references impossible.

**Decision tree:**
```
Is the game small enough for one scene?
├── YES → Monolithic
└── NO → Are there persistent systems across states?
    ├── NO → Scene-per-state (simple LoadScene)
    └── YES → Bootstrap + additive
              └── Is the world large/seamless? → Streaming chunks
```

## DECISION: Persistent Managers

**When this applies:** Systems that must survive scene transitions (audio, networking, input, game state).

**Options:**

1. **Bootstrap scene (recommended)** — managers live in a scene that is never unloaded.
   - Setup: Create a minimal scene with manager GameObjects. Set as scene 0 in build.
   - On start: bootstrap loads, then loads first content scene additively.
   - Benefit: managers are inspectable, debuggable, follow normal lifecycle.

2. **DontDestroyOnLoad** — mark specific GameObjects as persistent.
   - Use only when: you cannot control the initial scene load order.
   - Tradeoff: invisible in hierarchy after first load, confusing for new developers.
   - If you must: one root parent marked DDOL, all persistent objects as children.

3. **Static classes** — no GameObject, pure C# static state.
   - Use only when: data-only (no MonoBehaviour lifecycle needed).
   - Tradeoff: no Inspector visibility, no coroutines, no Update.

## DECISION: Scene Transitions

**When this applies:** Moving between game states or levels.

**Options:**

1. **Hard cut** — LoadScene (non-async). Screen goes blank briefly.
   - Use when: transitions are instant and small (menu → settings and back).

2. **Fade transition** — full-screen UI fades in, load happens behind, fade out.
   - Use when: clean transition without showing loading.
   - Implementation: persistent Canvas with fade panel in bootstrap scene.

3. **Loading screen** — separate loading scene with progress bar.
   - Use when: loads take > 1 second, need to show progress.
   - Pattern: Load loading-scene additively → unload old content → async load new content → unload loading-scene.

**MCP tool sequence for scene setup:**
1. `scene_create` — create bootstrap scene.
2. `scene_create_gameobject` — create manager objects.
3. `component_add` — add manager scripts.
4. `scene_create` — create first content scene.
5. `scene_set_build` — register both in build settings, bootstrap at index 0.

## Related Skills

- uniclaude:unity-architect — condensed scene decisions
- uniclaude:scene-authoring — MCP tool sequences for building scenes
