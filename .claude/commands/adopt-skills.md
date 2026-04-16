---
description: Adopt UniClaude's Unity skills into your project or global CLAUDE.md
---

You are helping the user adopt UniClaude's Unity development rules into their CLAUDE.md configuration.

## UniClaude Unity Skills

Below is the Unity guidance that ships with UniClaude. This is the content to be adopted:

<uniclaude-skills>
# Unity Development Rules

These rules apply when working in a Unity project with UniClaude installed.

## Unity Rules

- **Threading**: Unity API calls must happen on the main thread. Never use `Task.Run()`, `ThreadPool`, or background threads for Unity API calls.
- **Serialization**: Use `[SerializeField]` for private fields. Add `[System.Serializable]` to nested classes/structs. Properties are never serialized. `public` fields serialize by default — prefer `[SerializeField] private` instead.
- **Domain Reload**: Static fields reset when scripts recompile. Use `[InitializeOnLoad]` or `[InitializeOnEnterPlayMode]` to re-initialize. Use `SessionState` (session-scoped) or `EditorPrefs` (persistent) for Editor state that must survive reloads.
- **Lifecycle**: Execution order is `Awake` -> `OnEnable` -> `Start`. Do not assume `Start` has run inside `OnEnable`. Clean up subscriptions and resources in `OnDisable`/`OnDestroy`.
- **Unity Null**: Unity overloads `== null` for destroyed objects. Never use `is null` or `is not null` — these bypass the overload and return incorrect results for destroyed objects.
- **Editor vs Runtime**: Editor-only code must be in an `Editor/` assembly definition or guarded with `#if UNITY_EDITOR`. Never reference `UnityEditor` namespace from runtime assemblies.
- **Assembly Definitions**: Required for packages. They control compilation order and reference visibility. Editor-only asmdefs must have platform set to Editor only.

## MCP Tool Priority

Before modifying any Unity asset, scene, prefab, or component through file edits, check if a UniClaude MCP tool can do it directly. MCP tools handle undo registration, correct serialization, and domain reload batching — file edits do not.

If you need a Unity Editor action and are not sure which tool handles it, describe what you need and ask the user — do not guess with file edits.

### Scene

| I need to... | Tool |
|---|---|
| View scene hierarchy | `scene_get_hierarchy` |
| Create GameObjects (batch) | `scene_setup` |
| Create a single GameObject | `scene_create_gameobject` |
| Delete, rename, reparent GameObjects | `scene_delete_gameobject`, `scene_rename_gameobject`, `scene_reparent_gameobject` |
| Save, open, create, duplicate scenes | `scene_save`, `scene_open`, `scene_create`, `scene_duplicate` |
| Manage Build Settings scene list | `scene_list_build`, `scene_set_build` |

### Components

| I need to... | Tool |
|---|---|
| Inspect all components on a GameObject | `component_get_all` |
| Add or remove a component | `component_add`, `component_remove` |
| Find GameObjects by component type | `component_find` |
| Read a component property | `component_get_property` |
| Set component properties (batch) | `component_set_properties` |
| Set a single component property | `component_set_property` |

### Prefabs

| I need to... | Tool |
|---|---|
| Create a prefab from a scene object | `prefab_create` |
| Instantiate a prefab into the scene | `prefab_instantiate` |
| Inspect prefab contents without instantiating | `prefab_get_contents` |
| Multi-step prefab editing session | `prefab_open_editing` → use tools → `prefab_save_editing` |
| Edit a single prefab property | `prefab_edit_property` |
| Apply instance overrides to source prefab | `prefab_apply_overrides` |
| Create a prefab variant | `prefab_create_variant` |

### Inspector

| I need to... | Tool |
|---|---|
| Full property dump of a GameObject | `inspector_inspect` |
| Select/highlight a GameObject in Editor | `inspector_select` |

### Materials

| I need to... | Tool |
|---|---|
| Create a new material | `material_create` |
| Get or set shader properties | `material_get_properties`, `material_set_property` |
| Assign material to a renderer | `material_assign` |
| Duplicate a material | `material_duplicate` |
| Change a material's shader | `material_swap_shader` |

### Animation

| I need to... | Tool |
|---|---|
| Assign an AnimatorController to a GameObject | `animation_assign_controller` |
| Assign a clip to a controller state | `animation_assign_clip` |

### References & Events

| I need to... | Tool |
|---|---|
| Set an object reference field | `reference_set` |
| Read an object reference field | `reference_get` |
| Find all unset (null) references | `reference_find_unset` |
| Add a persistent UnityEvent listener | `event_add_listener` |
| Remove a UnityEvent listener | `event_remove_listener` |
| List listeners on a UnityEvent | `event_list_listeners` |
| Find all UnityEvent fields on a GameObject | `event_find_all` |

### Assets & Project

| I need to... | Tool |
|---|---|
| Search the project index (scripts, scenes, prefabs) | `project_search` |
| Find assets by filter (type, name) | `asset_find` |
| Get asset metadata (type, GUID, labels, deps) | `asset_get_info` |
| Move or rename an asset | `asset_move` |
| Force reimport an asset | `asset_import` |
| Refresh the AssetDatabase | `project_refresh_assets` |
| Read a project setting | `project_get_settings` |
| Run unit tests | `project_run_tests` |
| Get console log entries | `project_get_console_log` |

### Tags & Layers

| I need to... | Tool |
|---|---|
| List, create, or delete tags | `tag_list`, `tag_create`, `tag_delete` |
| List or create layers | `layer_list`, `layer_create` |

### Files & Scripts

| I need to... | Tool |
|---|---|
| Read or write a file | `file_read`, `file_write` |
| Find files by glob pattern | `file_find` |
| Delete a file (and its .meta) | `file_delete` |
| Create a C# script from template | `file_create_script` |
| Find-and-replace in a C# script | `file_modify_script` |
| Batch script edits (prevent per-file reload) | Wrap in `BeginScriptEditing` / `EndScriptEditing` |
</uniclaude-skills>

## Your Task

Follow these steps in order:

### Step 1: Read existing files

Read both of these files (if they exist):
- Project-root `CLAUDE.md` in the current working directory
- Global `~/.claude/CLAUDE.md`

### Step 2: Analyze and report

Compare the UniClaude skills above against whatever you found. Tell the user which case applies:

- **No existing CLAUDE.md**: You will create a new project-root `CLAUDE.md` containing the full UniClaude skills as a `## Unity (UniClaude)` section.
- **Existing file, no overlap**: You will append a `## Unity (UniClaude)` section to the project-root `CLAUDE.md` with the full content.
- **Existing file, partial overlap**: Identify which rules are already covered and which are new. You will add a `## Unity (UniClaude)` section containing only the rules not already present, and note which existing rules already cover UniClaude guidance.
- **Already fully covered**: Tell the user no changes are needed and stop here.

### Step 3: Ask for approval

Default target is the project-root `CLAUDE.md`. Ask the user:
- Whether they want to write to project-root (default) or global `~/.claude/CLAUDE.md` instead
- Show the exact content of the `## Unity (UniClaude)` section you will add
- Wait for explicit approval before writing anything

### Step 4: Write

Only after the user approves:
- If the target file does not exist, create it with the `## Unity (UniClaude)` section.
- If the target file exists, append the `## Unity (UniClaude)` section at the end.
- Keep all UniClaude content inside this single dedicated section — do not interleave into the user's existing structure.
- Confirm what was written and where.
