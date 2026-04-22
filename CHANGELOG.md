# Changelog

All notable changes to UniClaude will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/). Each released version carries a codename.


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
