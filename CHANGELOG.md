# Changelog

All notable changes to UniClaude will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/). Each released version carries a codename.


## [1.0.0] "Swan Song" - 2026-05-08

**Final release.** Anthropic's updated Terms of Service prohibit third-party tools from using subscription OAuth, which was UniClaude's core model. See [OutroMigration.md](OutroMigration.md) for context and MCP tool extraction plan.

### Added

- **Unity Agent persona.** Every conversation now includes a built-in system prompt that makes Claude a strict, professional Unity developer. The prompt documents all 75 MCP tools (organized by category with efficiency patterns), enforces design-first workflows, and instructs Claude to flag anti-patterns before acting.
- **Claude Code plugin registration.** The sidecar now registers itself as a local Claude Code plugin (`Sidecar~/.claude-plugin/plugin.json`), enabling the Agent SDK to discover and load UniClaude's skills automatically.
- **Skill tool enabled.** The `Skill` tool is now included in the Agent SDK's tool set, allowing Claude to invoke skills during conversations.
- **10 architecture skills.** A layered decision-making framework covering the full Unity development lifecycle:
  - **unity-architect** — top-level entry point with condensed decision trees for prefabs, scenes, components, data, and performance. Includes 5 deep-dive reference documents.
  - **unity-workflow** — design-first process: Analyze → Design → Implement → Verify.
  - **unity-reviewer** — post-implementation code review organized by severity (critical runtime bugs → performance → style).
  - **unity-performance** — CPU/GPU/RAM cost models, pooling strategies, batching patterns, and platform budgets.
  - **component-design** — MonoBehaviour composition, responsibility splitting, and communication patterns.
  - **data-modeling** — ScriptableObject patterns, runtime data management, and serialization strategies.
  - **prefab-architecture** — when to use separate prefabs vs. variants vs. SO config.
  - **prefab-workflow** — efficient MCP tool sequences for prefab authoring.
  - **scene-architecture** — scene organization, additive loading, persistent managers, and transitions.
  - **scene-authoring** — batch MCP tool sequences for building scenes.
  - **animation-workflow** — AnimatorController setup via MCP tools.

### Changed

- **System prompt composition.** The system prompt now combines project awareness context (Tier 1) with the Unity Agent persona, joined as separate sections. Previously only project context was appended.

### Tests

- **16 new test cases** covering plugin passthrough, skill tool inclusion, plan mode, undo/rewind, prompt suggestions, tool activity events (streaming and non-streaming), task tracking with parent linking, tool progress, and MCP connection management.

---

## [0.3.0] "Sharpening Tools" - 2026-05-01

### Added

- **Eager MCP connection with tool search.** The Unity MCP server connects at query start via the SDK's `mcpServers` config. The Agent SDK's built-in tool search automatically defers tool definitions from context, saving token overhead on conversations that don't use Unity tools. No meta-server or gateway pattern involved.
- **Direct MCP tool dispatch.** The MCP server now exposes all discovered tools directly via `tools/list` instead of routing through `search_unity_tools` / `call_unity_tool` meta-tools, reducing per-tool-call overhead by one round trip.
- **`scene_create_primitive` tool.** Creates primitive GameObjects (Cube, Sphere, Plane, Capsule, Cylinder, Quad) with optional position, rotation, scale, and parenting.
- **Asset import tools.** Three new MCP tools for reading and modifying asset import settings: `asset_get_import_settings` (generic read via SerializedObject), `asset_set_import_settings` (generic write + reimport), and `asset_set_clip_import_settings` (specialized for FBX animation clip loop time, loop pose, and frame range).
- **AnimatorController authoring tools.** Three new MCP tools for full state machine control: `animation_create_controller` (batch create with parameters, states, and transitions in one call), `animation_edit_controller` (batch add/remove operations on existing controllers), and `animation_get_controller` (inspect parameters, states, transitions, layers).

### Fixed

- **Domain reload watchdog.** After Unity's domain reload, the SSE stream could silently die while the sidecar query was still active. A watchdog now monitors SSE data flow and reconnects automatically if the stream goes silent for 10 seconds (up to 3 retries). If the query completed during reload, the UI transitions cleanly to idle.

## [0.2.0] "Ninja" - 2026-04-22

### Added

- **Ninja Install Mode.** Settings → Install Mode section lets you convert between Standard (UPM, team-visible) and Ninja (embedded, git-invisible) installs. In Ninja mode, `git status` stays clean while UniClaude runs normally. Reversible, with a Delete option that works in both modes.
- **Deterministic conversion flow with progress window.** Converting away from Ninja mode (Convert to Standard, Delete UniClaude) now opens a four-row checklist window — *Staging changes, Quitting Unity, Deleting package, Relaunching Unity* — that persists across the Unity restart via `EditorPrefs` and reopens on the next launch to show the terminal state. Replaces the previous 15-second blind-sleep approach with a detached `finalize-transition` helper that polls Unity's PID, deletes the embedded package, and relaunches Unity.
- **Version tracker in Settings tab.** A new section pinned to the top of Settings shows the installed version and polls GitHub's `releases/latest` once per day for newer releases. When an update is available, users can preview the release notes inline ("View changes") and trigger a one-click update. Ninja-mode updates run `git fetch --tags && git checkout <tag>` in the embedded clone via a progress window; standard-mode updates (tag-pinned Git URLs) rewrite `Packages/manifest.json` and delegate progress to Unity's Package Manager. Floating-ref and unknown install modes show the banner but disable the update button with an explanation.

### Fixed

- **macOS relaunch after Convert-to-Standard / Delete.** The finalize helper now translates Unity's `.app` bundle path to the inner `Contents/MacOS/Unity` binary before spawning. Previously the detached spawn silently failed on macOS because `.app` is a bundle directory, not an executable — Unity would quit and never come back.
- **UPM URL schemes with `git+` prefixes.** The installer correctly strips `git+file://`, `git+https://`, and `git+ssh://` prefixes before calling `git clone`, and hands `#ref` fragments to `--branch` instead of leaving them on the URL.
- **Setup overlay rendering on top of main UI after domain reload.** Tree building moved from `CreateGUI` into `OnEnable` (with a `rootVisualElement.Clear()` first), so the window rebuilds its tree on every reload instead of leaving an orphaned tree in `rootVisualElement` with null C# refs. Previously the deferred setup-state check couldn't find `_mainContainer` to hide and layered the setup card on top of the stale main UI.

## [0.1.0] - 2026-04-13

### Added

- **Editor chat window** — dockable Unity editor panel with streaming responses, dark/light theme, and font size options
- **Conversation history** — browse, search, and export past conversations (stored locally in `Library/UniClaude/`)
- **Project awareness** — automatic indexing of scripts, scenes, prefabs, shaders, ScriptableObjects, and project settings with two-tier context injection
- **MCP server** — JSON-RPC 2.0 server exposing 30+ Unity editor tools via the Model Context Protocol
- **Tool categories:** scene inspection, scene management, prefab editing, component management, material editing, animation, tag/layer management, file operations, project search, asset tools, inspector tools, and domain reload controls
- **Permission system** — tool calls require explicit user approval (allow once, allow for session, or deny)
- **Slash commands** — extensible command system with autocomplete, including `/healthcheck` for verifying the full pipeline
- **Domain reload resilience** — sidecar connection persists across Unity's domain reload cycle with auto/manual strategies
- **Settings persistence** — user preferences stored with atomic writes in `Library/UniClaude/settings.json`
- **Path safety** — `PathSandbox` validates all file paths stay within the project root and blocks writes to protected directories
- **Undo support** — scene and component tools integrate with Unity's undo system; file operations support undo via the sidecar
- **Model selection** — switch between Claude Sonnet, Opus, and Haiku with configurable reasoning effort
- **File attachments** — attach project files and screenshots to chat messages

### Known Limitations

- **Unity 6 only** — requires Unity 6000.3+. Unity 2022 LTS and 2023 LTS are not supported.
- **Local history only** — conversations are stored in `Library/` and do not sync between machines.
- **Node.js required** — the sidecar process requires Node.js 18+ installed on the host machine.
