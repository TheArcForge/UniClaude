---
name: unity-performance
description: "Use when you need detailed performance analysis — CPU/GPU/RAM cost models, optimization patterns, platform budgets, pooling, batching, and profiling strategies."
---

# Unity Performance — Full Decision Framework

Use this when unity-architect's condensed performance section needs more depth.

## Before Optimizing

Profile first. Never optimize based on assumptions.

**Tools:**
- Unity Profiler (CPU, GPU, Memory modules)
- Frame Debugger (draw calls, batching, overdraw)
- Memory Profiler package (heap snapshots, native allocations)
- project_get_console_log — check for warning spam (often a hidden perf cost)

**Rule:** Identify the bottleneck, then optimize that specific thing. Optimizing the wrong thing is wasted effort.

## CPU Cost Model

**Expensive (avoid in hot paths):**
- GetComponent<T>() — reflection-based lookup. Cache in Awake().
- GameObject.Find() / FindObjectOfType() — linear scene scan. Never in Update.
- string concatenation — allocates new string each time. Use StringBuilder.
- LINQ in Update — allocates enumerators and closures.
- Instantiate/Destroy — heavyweight. Pool for frequent use.
- Physics.Raycast (many per frame) — use NonAlloc variants, limit ray length.
- SendMessage / BroadcastMessage — reflection-based. Use direct calls or events.

**Cheap:**
- transform.position (cached by engine)
- Comparing tags (CompareTag, not ==)
- Null checks on Unity objects (but NOT ?? or ?. — those bypass Unity's null check)
- Static method calls, struct operations
- Fixed-size arrays, pre-allocated lists

## GPU Cost Model

**Draw calls (CPU→GPU overhead per rendered object):**
- Each unique material = at least one draw call.
- Reduce by: batching (static/dynamic/GPU instancing/SRP batcher), atlasing textures.
- Target: < 100 on mobile, < 2000 on console, < 5000 on PC.

**Fill rate (per-pixel cost):**
- Transparent objects drawn back-to-front, no early-Z rejection.
- Overdraw: overlapping transparent objects multiply pixel cost.
- Reduce by: smaller particles, opaque where possible, LOD.

**Shader complexity:**
- Per-vertex operations are cheap (scale with mesh complexity).
- Per-pixel operations are expensive (scale with screen resolution).
- Texture samples per pixel: each costs bandwidth. Combine maps.

## Memory Cost Model

**Textures (usually largest):**
- 2048x2048 RGBA uncompressed = 16MB.
- With ASTC 4x4 compression = ~4MB.
- With mipmaps: +33% memory.
- Rule: smallest resolution that looks acceptable. Check with asset_get_import_settings.

**Meshes:**
- 10K vertices ≈ 1MB (with normals, UVs, tangents).
- Share meshes across instances (instancing) — one copy in memory.

**Audio:**
- 1 minute stereo WAV = ~10MB.
- Vorbis compressed = ~1MB.
- Decompress On Load: uses decompressed size in memory (fast playback).
- Compressed In Memory: uses compressed size (slower playback, less RAM).

## DECISION: Object Pooling

**When to pool:**
- Objects spawned and destroyed more than 10/second.
- On mobile: pool anything spawned during gameplay.
- GC spikes visible in profiler correlating with instantiate/destroy.

**When NOT to pool:**
- Objects created once and persist (singletons, managers, UI panels).
- < 5 spawns per second on PC/console — instantiate is simpler.
- Objects that require unique initialization that can't be reset.

**Pool implementation checklist:**
1. Pre-warm during loading (not during gameplay).
2. Get() → SetActive(true) + reset all state.
3. Return() → SetActive(false) + clear references.
4. Cap size — destroy overflow to prevent unbounded growth.
5. Stack<T> (LIFO) for cache friendliness.

## DECISION: Rendering Optimization

**Static batching:**
- Mark non-moving objects as Static (Batching Static flag).
- Use for: environment, props, decorations.
- Cost: higher memory (mesh data duplicated for batching).
- Set via inspector_inspect to check current static flags.

**GPU Instancing:**
- For many identical meshes (trees, grass, particles).
- Enable on the material: material_set_property("_EnableInstancing", true).
- Objects can have different transforms but must share mesh+material.

**SRP Batcher (URP/HDRP):**
- Enabled by default. Works for same-shader, different-material objects.
- Most impactful for diverse scenes with many different materials.

**Sprite atlases:**
- Combine sprites into atlases to reduce draw calls.
- One atlas = one material = batchable.
- Separate atlases by scene/frequency-of-use to avoid loading unused sprites.

## DECISION: Physics Optimization

**Collision matrix:**
- Set up layers so objects that shouldn't collide never check.
- Fewer collision pairs = less CPU time in physics step.
- Use layer_create + project_get_settings to verify setup.

**Collider choice:**
- Sphere/Capsule/Box: cheapest.
- Mesh Collider (convex): moderate.
- Mesh Collider (non-convex): expensive. Avoid on moving objects.

**Physics settings:**
- Fixed Timestep: default 0.02 (50 Hz). Increase for less precision, less CPU.
- Solver iterations: reduce for less accuracy, faster simulation.

## Related Skills

- uniclaude:unity-architect — condensed performance decisions
- uniclaude:unity-reviewer — catches performance anti-patterns in code review
