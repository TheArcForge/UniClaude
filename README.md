# UniClaude

[![Version](https://img.shields.io/badge/version-0.1.0-blue)](CHANGELOG.md)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Unity 6+](https://img.shields.io/badge/Unity-6000.3%2B-black)](https://unity.com)

Claude Code, natively inside Unity Editor. A dockable chat window with full project awareness, 60+ MCP tools, and zero alt-tabbing — all powered by your Claude subscription.

## Features

- **Editor chat window** — dockable panel with streaming responses, dark/light theme, and conversation history
- **Project awareness** — UniClaude indexes your scenes, prefabs, scripts, shaders, and ScriptableObjects so Claude understands your project structure
- **60+ Unity tools** — Claude can inspect and modify scenes, prefabs, components, materials, animations, tags, layers, and project settings through the MCP protocol — all with full Undo integration
- **Model selection** — choose between Sonnet, Opus, and Haiku with configurable reasoning effort (Low → Max)
- **File & image attachments** — attach project files and screenshots directly in the chat input
- **Permission system** — every tool call requires explicit approval (allow once, allow for session, or deny)
- **Slash commands** — extensible command system with autocomplete (`/healthcheck`, `/clear`, `/export`, and more)
- **Diff viewer** — review script edits in a colored diff popup (green/red lines) without leaving UniClaude
- **Settings panel** — configurable font size, Node.js path, sidecar port, context token budget, and package filtering
- **MCP server** — JSON-RPC 2.0 server that exposes Unity editor actions to any MCP-compatible client
- **Domain reload resilience** — survives Unity's domain reload cycle with automatic reconnection

## Requirements

| Requirement | Version |
|-------------|---------|
| Unity | **6000.3+** (Unity 6) |
| Node.js | **18+** |
| Anthropic API key | Set as `ANTHROPIC_API_KEY` environment variable |

## Installation

Add via Unity Package Manager using the git URL:

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter:
   ```
   https://github.com/ArcForge/com.arcforge.uniclaude.git
   ```
4. Click **Add**

## Quick Start

1. Open the UniClaude window: **ArcForge > UniClaude**
2. On first launch, UniClaude will build its Node.js sidecar automatically (requires Node.js 18+ on your PATH)
3. Set your Anthropic API key as an environment variable: `export ANTHROPIC_API_KEY=sk-ant-...`
4. Start chatting — Claude can see your project structure and use editor tools with your permission

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
