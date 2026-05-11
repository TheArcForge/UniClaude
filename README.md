# UniClaude

![UniClaude](Documentation/UniClaudeGIF.gif)

> **Project Sunset Notice** — v1.0.0 "Swan Song" is the final release of UniClaude. Anthropic's [updated Terms of Service](https://www.anthropic.com/policies) now prohibit third-party tools from authenticating via Claude subscription OAuth, which was UniClaude's core pricing model. See [OutroMigration.md](OutroMigration.md) for context and a plan for extracting UniClaude's MCP tools into a standalone, client-agnostic package. Thank you to everyone who used and contributed to this project.

**[Technical Overview](OVERVIEW.md)** — architecture, engineering challenges, and project scope at a glance.

[![Version](https://img.shields.io/badge/version-1.0.0%20%22Swan%20Song%22-blue)](CHANGELOG.md)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Unity 6+](https://img.shields.io/badge/Unity-6000.3%2B-black)](https://unity.com)

Claude Code, natively inside Unity Editor. A dockable chat window with full project awareness, 75+ MCP tools, an opinionated Unity architecture advisor, and zero alt-tabbing — all powered by your Claude subscription.

## Features

- **Editor chat window** — dockable panel with streaming responses, dark/light theme, and conversation history
- **Unity Agent persona** — a built-in system prompt that makes Claude a strict, professional Unity developer. It documents all 75 MCP tools with efficiency patterns, enforces design-first workflows, and flags anti-patterns before they ship.
- **10 architecture skills** — Claude Code skills covering the full Unity development lifecycle: architecture decisions, component design, data modeling, scene/prefab authoring, animation workflows, performance optimization, and code review. Skills load on demand via the Agent SDK's plugin system.
- **Project awareness** — UniClaude indexes your scenes, prefabs, scripts, shaders, and ScriptableObjects so Claude understands your project structure
- **75+ Unity tools** — Claude can inspect and modify scenes, prefabs, components, materials, animations, tags, layers, and project settings through the MCP protocol — all with full Undo integration
- **Model selection** — choose between Sonnet, Opus, and Haiku with configurable reasoning effort (Low → Max)
- **File & image attachments** — attach project files and screenshots directly in the chat input
- **Permission system** — every tool call requires explicit approval (allow once, allow for session, or deny)
- **Slash commands** — extensible command system with autocomplete (`/healthcheck`, `/clear`, `/export`, and more)
- **Diff viewer** — review script edits in a colored diff popup (green/red lines) without leaving UniClaude
- **Settings panel** — configurable font size, Node.js path, sidecar port, context token budget, and package filtering
- **Ninja Install Mode** — optional embedded-clone install that hides UniClaude from `git status` and `packages-lock.json` on team projects. Reversible from Settings.
- **Version tracker** — Settings tab surfaces new releases from GitHub once a day, with inline changelog preview and one-click update (mode-aware for Ninja and Standard installs).
- **MCP server** — JSON-RPC 2.0 server that exposes Unity editor actions to any MCP-compatible client
- **Domain reload resilience** — survives Unity's domain reload cycle with automatic reconnection

## Requirements

| Requirement | Version |
|-------------|---------|
| Unity | **6000.3+** (Unity 6) |
| Node.js | **18+** |
| Claude subscription | Pro or Max plan (authenticates via Claude Code's OAuth) |

## Installation

Add via Unity Package Manager using the git URL:

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter:
   ```
   https://github.com/TheArcForge/UniClaude.git#v1.0.0
   ```
4. Click **Add**

## Quick Start

1. Open the UniClaude window: **ArcForge > UniClaude**
2. On first launch, UniClaude will build its Node.js sidecar automatically (requires Node.js 18+ on your PATH)
3. Ensure you're logged into Claude Code (`claude` CLI) — UniClaude authenticates through your Claude subscription
4. Start chatting — Claude can see your project structure, invoke architecture skills, and use editor tools with your permission

### Key Controls

| Key | Action |
|-----|--------|
| Enter | Send message |
| Shift+Enter | New line |
| Escape | Cancel generation |
| Tab | Accept slash command autocomplete |

## Documentation

- [Setup Guide](docs/SETUP.md) — detailed installation, configuration, and troubleshooting
- [Architecture](docs/ARCHITECTURE.md) — package structure, data flow, and extension points

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, running tests, and pull request guidelines.

## Security

To report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE)
