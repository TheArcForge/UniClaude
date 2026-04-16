# Changelog

All notable changes to UniClaude will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

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
