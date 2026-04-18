# Setup Guide

## Prerequisites

### Unity 6

UniClaude requires **Unity 6000.3 or later** (Unity 6). It is an Editor-only package — it does not ship in builds or affect runtime performance.

### Node.js

UniClaude uses a Node.js sidecar process to communicate with the Claude API via the Anthropic Agent SDK. You need **Node.js 18 or later** installed.

**Check your version:**
```bash
node --version
# Expected: v18.x or higher
```

**Install Node.js** (if not installed):
- **macOS (Homebrew):** `brew install node`
- **Windows (Scoop):** `scoop install nodejs`
- **All platforms:** Download from [nodejs.org](https://nodejs.org/)

UniClaude searches for Node.js in the following order:
1. Custom path configured in Settings (if set)
2. System PATH
3. Platform-specific locations:
   - macOS: `/usr/local/bin`, Homebrew (`/opt/homebrew/bin`), nvm
   - Windows: `Program Files\nodejs`, Scoop, nvm, fnm

### Anthropic API Key

You need an Anthropic API key with access to Claude. Set it as an environment variable:

```bash
# macOS / Linux — add to your shell profile (~/.zshrc, ~/.bashrc, etc.)
export ANTHROPIC_API_KEY=sk-ant-your-key-here

# Windows (PowerShell)
$env:ANTHROPIC_API_KEY = "sk-ant-your-key-here"
```

The key is never stored by UniClaude — it is read from the environment by the Node.js sidecar at runtime.

## Installation

### Via Git URL (recommended)

1. Open your Unity 6 project
2. Go to **Window > Package Manager**
3. Click the **+** button > **Add package from git URL...**
4. Enter:
   ```
   https://github.com/TheArcForge/UniClaude.git
   ```
5. Click **Add**

Unity will clone the repository and register the package. This may take a moment.

### Via Local Path (for development)

1. Clone the repository:
   ```bash
   git clone https://github.com/TheArcForge/UniClaude.git
   ```
2. In Unity, go to **Window > Package Manager**
3. Click **+** > **Add package from disk...**
4. Select the `package.json` file in the cloned directory

## First Run

1. Open the UniClaude window: **ArcForge > UniClaude**
2. UniClaude will detect the sidecar and run `npm install` to install dependencies. This happens once and takes 10-30 seconds depending on your connection.
3. The sidecar process starts automatically and connects on a localhost port.
4. You should see the chat interface with a welcome message and suggestions.

If the sidecar fails to start, run `/healthcheck` in the chat input to diagnose the issue, or check the [Troubleshooting](#troubleshooting) section below.

## Configuration

Open the **Settings** tab in the UniClaude window to configure:

### Model Selection

| Model | Best for |
|-------|----------|
| Sonnet 4.6 (default) | Fast responses, everyday tasks |
| Opus 4.6 | Complex reasoning, large refactors |
| Haiku 4.5 | Quick questions, simple lookups |

### Reasoning Effort

Controls how much reasoning Claude applies to each response:
- **Low** — quick, concise answers
- **Medium** — balanced
- **High** — thorough analysis (default)
- **Max** — maximum reasoning depth

### Project Awareness

When enabled, UniClaude indexes your project's assets and injects context into each conversation. This helps Claude understand your project structure without you having to explain it.

You can configure:
- **Excluded folders** — directories to skip during indexing
- **Package overrides** — include or exclude specific UPM packages from the index

Use **Full Index Rebuild** to force a full re-index, or **Clear Index** to remove the cached index.

### Other Settings

- **Font size** — small, medium, large, or extra-large
- **Node.js path** — override the auto-detected Node.js binary location
- **Sidecar port** — set a fixed port (0 = auto-assign)
- **Verbose logging** — enable detailed sidecar logs for debugging
- **Restart Sidecar** — kills the current sidecar process and spawns a fresh one. Useful after updating sidecar code or when the process gets into a bad state
- **Context token budget** — maximum tokens for the project tree summary sent with every message. Lower values reduce cost but give Claude less project visibility. Default: 3300 (~$0.01/message at Sonnet pricing). Set to 0 for unlimited (full tree).
- **Auto-allow MCP tools** — skip permission prompts for UniClaude's built-in tools

## MCP Server

UniClaude includes a built-in MCP (Model Context Protocol) server that exposes Unity editor actions. The server starts automatically with the sidecar and listens on localhost.

### Domain Reload Strategy

Unity's domain reload (triggered by script compilation) temporarily disconnects the MCP server. Two strategies are available:

- **Auto (default)** — pauses tool execution during reload, resumes automatically. Includes a 120-second safety timeout.
- **Manual** — holds execution until you explicitly resume. Use this if auto-resume causes issues with your workflow.

## Troubleshooting

### Run the health check

Type `/healthcheck` in the chat input to run a diagnostic pipeline that verifies Node.js, sidecar connectivity, and MCP tool execution. It reports pass/fail for each step and is the fastest way to pinpoint what's broken.

### macOS Gatekeeper blocks sidecar binaries

**Symptoms:** "Apple could not verify X is free of malware" popups when UniClaude starts.

The sidecar's npm dependencies include unsigned native binaries (e.g., `ripgrep` bundled with the Claude Agent SDK). macOS quarantines these after download. UniClaude's `postinstall` script clears the quarantine flag automatically during setup, but if you installed dependencies manually or the flag persists:

```bash
xattr -dr com.apple.quarantine Packages/com.arcforge.uniclaude/Sidecar~/node_modules/
```

This is safe — the binaries originate from the `@anthropic-ai/claude-agent-sdk` npm package. The issue is tracked upstream in the SDK.

### Sidecar won't start

**Symptoms:** "Sidecar not running" in the status bar, chat doesn't respond.

1. **Check Node.js:** Run `node --version` in your terminal. Must be 18+.
2. **Check PATH:** Unity may not inherit your shell's PATH. Try setting an explicit Node.js path in Settings.
3. **Check dependencies:** Delete `Packages/com.arcforge.uniclaude/Sidecar~/node_modules/` and reopen the UniClaude window to trigger a fresh `npm install`.
4. **Check API key:** Ensure `ANTHROPIC_API_KEY` is set in your environment. Unity inherits environment variables from the process that launched it — if you set the key after launching Unity, restart Unity.

### Connection drops during use

UniClaude automatically reconnects with exponential backoff (1s, 2s, 4s, 8s — up to 3 attempts). When reconnecting after a domain reload, the SSE stream uses `Last-Event-ID` to replay any events that were missed, so in-flight queries resume seamlessly.

If reconnection fails:

1. Click **Restart Sidecar** in Settings to kill and respawn the sidecar process
2. If that doesn't work, close and reopen the UniClaude window
3. Check if the sidecar process is still running (look for a `node` process on the configured port)

### Domain reload breaks tools

If MCP tool calls hang after a script recompilation:

1. Try switching the domain reload strategy in Settings
2. The auto strategy has a 120-second timeout — if it hasn't resolved by then, the lock is released automatically

### Conversation history missing

Conversations are stored locally in `Library/UniClaude/`. This directory is:
- **Not version-controlled** (Unity's `Library/` is git-ignored by convention)
- **Machine-local** — conversations don't sync between machines

This is by design — conversation data stays on your machine and is never transmitted except to the Anthropic API during active chats.
