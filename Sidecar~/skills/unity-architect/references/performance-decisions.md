# Performance — Deep Dive

Supplements the DECISION: Performance Strategy block in the parent SKILL.md.

## Optimization Priority Order

Always profile before optimizing. Optimizing the wrong thing wastes time.

1. **Algorithmic** — O(n^2) to O(n), unnecessary work removal. Biggest wins.
2. **Allocation reduction** — remove GC pressure in hot paths. Struct over class for temp data.
3. **Batching** — reduce API call overhead (draw calls, physics queries, component lookups).
4. **Caching** — store results of expensive lookups (GetComponent, Find, string operations).
5. **LOD/culling** — don't process what the player can't see.

## Per-Platform Budgets (Approximate)

| Metric | Mobile | Console | PC |
|--------|--------|---------|-----|
| Draw calls | < 100 | < 2000 | < 5000 |
| Triangles/frame | < 100K | < 2M | < 5M |
| SetPass calls | < 50 | < 200 | < 500 |
| Target frametime | 33ms (30fps) | 16ms (60fps) | 8ms (120fps) |

## CPU Optimization Patterns

**Caching:**
```
// WRONG: every frame
void Update() { GetComponent<Rigidbody>().AddForce(dir); }

// RIGHT: cached
Rigidbody _rb;
void Awake() { _rb = GetComponent<Rigidbody>(); }
void Update() { _rb.AddForce(dir); }
```

**Avoid allocations in Update:**
- No new() for classes in Update.
- No string concatenation (use StringBuilder or interpolation only for one-shot).
- No LINQ in hot paths (allocates enumerators).
- No foreach on non-struct enumerators.
- Use NonAlloc variants: Physics.RaycastNonAlloc, Physics.OverlapSphereNonAlloc.

**Reduce work frequency:**
- Not everything needs to run every frame. Use timers for periodic checks.
- Stagger expensive operations across frames (process 10 enemies per frame, not all 200).

## GPU Optimization Patterns

**Draw call reduction:**
- Static batching: mark non-moving renderers as Static.
- GPU instancing: enable on materials with many identical meshes.
- SRP batcher: enabled by default in URP/HDRP for different-material same-shader objects.
- Texture atlases: combine sprites into atlases to reduce material swaps.

**Overdraw:**
- Avoid large transparent overlapping surfaces.
- Use opaque materials where possible (cheaper sorting).
- Particle systems: reduce particle count, increase size.

**Shader complexity:**
- Mobile: avoid per-pixel lighting on many objects. Use baked lighting.
- Reduce texture samples per pixel. Combine maps (metallic + smoothness in one texture).

## Memory Optimization Patterns

**Textures:**
- Use appropriate compression (ASTC for mobile, BC7 for PC).
- Set max resolution per platform in import settings (asset_set_import_settings).
- Mipmap streaming for large open worlds.

**Audio:**
- Compress in Vorbis for music, ADPCM for short SFX.
- Load On Demand for large clips, Decompress On Load for frequent short clips.

**Meshes:**
- Reduce poly count for distant objects (LOD groups).
- Share meshes across instances (GPU instancing).

## Object Pooling Checklist

When implementing a pool:
1. Pre-allocate during loading, not during gameplay.
2. Deactivate (SetActive(false)) instead of Destroy.
3. Reset ALL state on reuse (position, components, flags, timers).
4. Cap pool size — destroy overflow to prevent memory leaks.
5. Use a Stack<T> (LIFO) — recently used objects are more likely to be warm in cache.
