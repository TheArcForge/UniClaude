# Scene Architecture — Deep Dive

Supplements the DECISION: Scene Architecture block in the parent SKILL.md.

## Additive Loading Patterns

**Bootstrap scene (always loaded first):**
- Contains persistent managers (AudioManager, InputManager, NetworkManager).
- Loads the first content scene additively on start.
- Use scene_create to create this as a minimal scene.

**Content scenes (loaded/unloaded dynamically):**
- Each level, menu, or game state is its own scene.
- SetActiveScene to the content scene so new GameObjects spawn there.
- Unload previous content scene before loading next (or keep both for transitions).

## DontDestroyOnLoad Alternatives

- Avoid DontDestroyOnLoad — it creates invisible persistence that's hard to debug.
- Prefer: bootstrap scene pattern (managers live in a scene that's never unloaded).
- If you must use it: one single root GameObject that parents all persistent objects.

## Scene Transitions

- Fade-to-black: coroutine that fades a full-screen UI panel, loads scene, fades back.
- Loading screen: additive load a loading scene, unload old content, load new content, unload loading.
- For both: never assume scene loads are instant. Use async loading with progress.

## Scene Dependencies

- A scene should not assume any other scene is loaded — check for managers, don't crash without them.
- Cross-scene references don't serialize — use asset references (SOs, addressables) instead.
- Test each scene by loading it directly (Play Mode from that scene) — if it breaks, it has hidden dependencies.

## Build Settings

- Use scene_list_build to verify which scenes are registered.
- Use scene_set_build to update the build list.
- Scene index 0 is always loaded first in builds — should be your bootstrap scene.
