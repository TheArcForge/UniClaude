# Architecture

## Package Structure

```
com.arcforge.uniclaude/
├── Editor/                              # All C# code (editor-only)
│   ├── Core/                            # Data models, services, indexing
│   │   ├── Scanners/                    # Asset type scanners (script, scene, prefab, etc.)
│   │   ├── ActivityLog.cs               # Hierarchical tool/task execution tracking
│   │   ├── ChatMessage.cs               # Message model (role, content, timestamp, activity)
│   │   ├── Conversation.cs              # Conversation model (messages, sessionId, title)
│   │   ├── ConversationStore.cs         # Conversation persistence (Library/UniClaude/)
│   │   ├── ContextFormatter.cs          # Two-tier context string builder
│   │   ├── HealthCheckRunner.cs         # Diagnostic pipeline runner
│   │   ├── HealthCheckStep.cs           # Individual health check step
│   │   ├── HealthCheckSteps.cs          # Built-in check implementations
│   │   ├── IAssetScanner.cs             # Scanner interface
│   │   ├── IIndexRetriever.cs           # Retriever interface
│   │   ├── IndexEntry.cs               # Index data structure
│   │   ├── IndexFilterSettings.cs       # Package inclusion/exclusion logic
│   │   ├── KeywordRetriever.cs          # Token-based query scoring and ranking
│   │   ├── PackageDiscovery.cs          # UPM package detection
│   │   ├── PathSandbox.cs              # Path traversal protection
│   │   ├── ProjectAwareness.cs          # Project indexing orchestrator
│   │   ├── ProjectIndexStore.cs         # Index cache persistence
│   │   ├── SidecarClient.cs             # HTTP + SSE client for the Node.js bridge
│   │   ├── SidecarManager.cs            # Sidecar process lifecycle management
│   │   ├── SlashCommandRegistry.cs      # Slash command discovery and execution
│   │   ├── StreamPhase.cs               # Agent reasoning phase enum
│   │   ├── TokenUsage.cs                # Token count and cost tracking
│   │   ├── UniClaudeAssetPostprocessor.cs # Asset change detection for incremental indexing
│   │   └── UniClaudeSettings.cs         # Persistent user settings
│   ├── MCP/                             # Model Context Protocol server
│   │   ├── Tools/                       # Tool implementations (14 categories, 60+ tools)
│   │   ├── DomainReload/                # Domain reload strategies (auto/manual)
│   │   ├── Transport/                   # HTTP transport layer
│   │   ├── MCPServer.cs                 # Server lifecycle and main-thread dispatch
│   │   ├── MCPDispatcher.cs             # Reflection-based tool discovery and routing
│   │   ├── MCPSettings.cs               # MCP server configuration (EditorPrefs)
│   │   ├── MCPToolAttribute.cs          # [MCPTool] and [MCPToolParam] attributes
│   │   └── MCPToolResult.cs             # Tool execution result type
│   ├── Installer/                       # Install-mode conversion (Ninja ↔ Standard)
│   │   ├── GitCli.cs                    # Thin synchronous git CLI wrapper
│   │   ├── InstallMode.cs               # Ninja/Standard/Other enum
│   │   ├── InstallModeProbe.cs          # Detects current install mode
│   │   ├── InstallModeSection.cs        # Settings UI for conversion/deletion
│   │   ├── InstallerBridge.cs           # Orchestrates Unity → Node installer handoff
│   │   ├── InstallerPostReload.cs       # Post-reload continuation hooks
│   │   ├── PendingTransitionMarker.cs   # Persisted transition state (survives domain reload)
│   │   ├── TransitionKind.cs            # ToNinja / ToStandard / DeleteFromNinja
│   │   ├── TransitionProgressWindow.cs  # Four-row checklist window for conversions
│   │   └── TransitionStatus.cs          # Status payload written by the Node helper
│   ├── UI/                              # Editor window components
│   │   ├── Input/                       # Chat input subsystem
│   │   │   ├── AttachmentChip.cs        # Attachment pill UI element
│   │   │   ├── AttachmentChipStrip.cs   # Attachment strip layout
│   │   │   ├── AttachmentInfo.cs        # Attachment data model
│   │   │   ├── AttachmentManager.cs     # File/image validation and staging
│   │   │   ├── ChatInputField.cs        # Input field with markdown rendering
│   │   │   ├── InputController.cs       # Keyboard shortcuts and event routing
│   │   │   └── MessageSubmission.cs     # Message preparation and validation
│   │   ├── UniClaudeWindow.cs           # Main EditorWindow (thin orchestrator)
│   │   ├── ChatPanel.cs                 # Chat message display and streaming
│   │   ├── DiffViewerWindow.cs          # Colored diff popup for script edits
│   │   ├── HistoryPanel.cs              # Conversation history browser
│   │   ├── SettingsPanel.cs             # Settings UI
│   │   ├── PermissionPromptElement.cs   # Tool approval overlay
│   │   ├── ThemeContext.cs              # Dark/light theme tokens
│   │   ├── ThinkingIndicator.cs         # Spinner animation for streaming
│   │   └── MessageRenderer.cs           # Markdown-to-VisualElement rendering
│   └── VersionTracker/                  # GitHub release polling + one-click update
│       ├── CheckResult.cs               # CheckStatus enum + result snapshot
│       ├── GitHubReleaseFetcher.cs      # Real HttpClient implementation
│       ├── IReleaseFetcher.cs           # Fetcher interface + FetchResult
│       ├── ManifestEditor.cs            # Pure manifest.json inspect + rewrite
│       ├── NinjaUpdater.cs              # Ninja-mode updater + progress window
│       ├── SemverCompare.cs             # Semver parse + compare
│       ├── StandardUpdater.cs           # Standard-mode manifest rewrite + Client.Resolve
│       ├── VersionCheckService.cs       # Orchestrator with 24h cache
│       └── VersionTrackerSection.cs     # Settings-tab VisualElement with 4 states
├── Tests/                               # Unit and integration tests (36+ fixtures)
├── Sidecar~/                            # Node.js bridge (Agent SDK, HTTP server)
│   └── src/
│       ├── index.ts                     # Entry point (argument parsing)
│       ├── server.ts                    # Express server (/chat, /health, /approve, /deny, /cancel, /undo)
│       ├── agent.ts                     # AgentRunner (Agent SDK orchestration)
│       ├── types.ts                     # Request/response and SSE event types
│       ├── permissions.ts               # SessionTrust (per-session tool trust)
│       └── plugins.ts                   # Plugin discovery
├── Installer~/                          # Node.js installer helpers (Ninja mode conversions)
│   ├── installer.mjs                    # Entry point (to-ninja, to-standard, delete-from-ninja, finalize-transition)
│   ├── src/                             # Command implementations and shared utilities
│   └── tests/                           # Node test suite (vitest-style assertions)
├── Skills~/                             # Claude Code skill definitions
├── docs/                                # Documentation
├── package.json                         # UPM manifest
└── LICENSE                              # MIT license
```

Directories ending in `~` are ignored by Unity's asset pipeline but included in the package.

## Core Concepts

### Two-Process Architecture

UniClaude uses a **sidecar pattern**: the Unity editor (C#) communicates with a Node.js process over localhost HTTP.

```
Unity Editor (C#)                    Node.js Sidecar
┌─────────────────┐                 ┌──────────────────┐
│ UniClaudeWindow  │                 │ Anthropic Agent   │
│ ├─ ChatPanel     │   HTTP POST    │ SDK               │
│ ├─ SidecarClient ├───────────────>│ ├─ /chat          │
│ │                │   SSE stream   │ ├─ /stream        │
│ │                │<───────────────┤ ├─ /health        │
│ │                │                │ ├─ /approve       │
│ │                │                │ ├─ /deny          │
│ │                │                │ ├─ /cancel        │
│ └─ MCPServer     │                │ └─ /undo          │
│    ├─ Dispatcher │   JSON-RPC     │                    │
│    └─ Transport  │<───────────────┤ MCP client         │
└─────────────────┘                 └──────────────────┘
```

**Why a sidecar?** The Anthropic Agent SDK is a Node.js library. Running it in-process would require embedding a JS runtime in Unity, which is impractical. The sidecar pattern keeps the Unity side pure C# while giving full access to the Agent SDK.

### MCP (Model Context Protocol)

The MCP server exposes Unity editor actions as tools that Claude can call. All discovered tools are listed directly in `tools/list` and dispatched directly by name in `tools/call`. The flow:

1. Claude decides to call a tool (e.g., `scene_get_hierarchy`)
2. The Agent SDK sends a JSON-RPC request to the MCP server
3. `MCPDispatcher` routes the request to the matching `[MCPTool]` method
4. The tool executes on Unity's main thread (via `EditorApplication.update` queue)
5. The result is returned to Claude through the same JSON-RPC channel

Tools are discovered via reflection at startup — any static method with `[MCPTool]` is automatically registered.

### Lazy Tool Activation

To avoid paying ~14k tokens of tool schema overhead on conversations that don't need editor tools, the sidecar uses lazy activation:

1. At conversation start, only a single `enable_unity_tools` meta-tool is registered via an in-process SDK MCP server (`uniclaude-meta`)
2. For generic questions (explain, review, design), the model answers without calling it — cost: ~200 tokens
3. When the model needs editor access, it calls `enable_unity_tools`
4. The handler calls `Query.setMcpServers()` to dynamically connect the Unity HTTP MCP server mid-turn
5. The SDK fetches `tools/list` (69 tools) and the model continues the same turn with full toolset

Once activated, tools stay available for the rest of the conversation. New conversations start lightweight again.

### Project Awareness

UniClaude indexes the project to give Claude context about what it's working with. The pipeline:

1. **Scanners** (`IAssetScanner` implementations) parse asset files and produce `IndexEntry` records
2. The **index** stores entries with names, symbols, dependencies, and summaries
3. A **retriever** (`IIndexRetriever`) matches user queries against the index using keyword scoring
4. A **formatter** (`ContextFormatter`) builds the context string injected into Claude's system prompt

Context is injected in two tiers:
- **Tier 1** — always included: project tree summary (scripts, scenes, prefabs, shaders), capped by `ContextTokenBudget`. The tree is expanded breadth-first; when the budget is reached, remaining folders are summarized as one-liners (e.g., "Assets/Scripts/AI/ — 23 files (14 .cs, 9 .prefab)")
- **Tier 2** — per-message: keyword-matched files relevant to the user's query

## Data Flow

### Chat Message Lifecycle

```
User types message
    │
    ▼
InputController → UniClaudeWindow.StartChat()
    │
    ▼
SidecarClient.StartChat(message, model, effort, attachments)
    │  HTTP POST /chat
    ▼
Node.js sidecar → Anthropic Agent SDK → Claude API
    │
    │  SSE stream back
    ▼
SidecarClient dispatches events (each event carries an incrementing id):
    ├─ OnToken             → ChatPanel streams text
    ├─ OnPhaseChanged      → ThinkingIndicator updates (thinking/writing/tool_use)
    ├─ OnToolActivity      → Activity tracker logs tool invocation
    ├─ OnToolProgress      → Elapsed time tracking per tool call
    ├─ OnPermissionRequest → PermissionPromptElement shown
    ├─ OnTaskEvent         → Subagent task tracking (started/completed/failed)
    └─ OnResult            → Final message rendered, conversation saved to disk

On reconnect (e.g., after domain reload), SidecarClient sends Last-Event-ID
to replay missed events. The sidecar buffers all events for the current query
and replays any with id > Last-Event-ID before resuming the live stream.
```

### MCP Tool Call Lifecycle

```
Claude requests tool call (e.g., scene_get_hierarchy)
    │
    ▼
Agent SDK → JSON-RPC tools/call → HttpTransport
    │
    ▼
MCPServer.EnqueueAndWait() → main thread queue
    │
    ▼
ProcessMainThreadQueue() on EditorApplication.update
    │
    ▼
MCPDispatcher.HandleToolCall(toolName, args)
    │  Direct dispatch to registered tool
    ▼
[MCPTool] static method executes
    │
    ▼
MCPToolResult → JSON-RPC response → Agent SDK → Claude
```

## Permission System

Every MCP tool call requires explicit user approval. The permission system spans both the C# frontend and the Node.js sidecar.

### Flow

```
Claude requests tool call
    │
    ▼
Agent SDK → canUseTool callback (agent.ts)
    │
    ├─ SessionTrust.isTrusted(toolName)?
    │   ├─ yes → auto-approve, tool executes immediately
    │   └─ no  → emit "permission_request" SSE event
    │               │
    │               ▼
    │           SidecarClient → PermissionPromptElement shown in ChatPanel
    │               │
    │               ▼
    │           User chooses:
    │               ├─ Allow Once  → POST /approve (one-time)
    │               ├─ Allow Session → POST /approve + SessionTrust.trust(toolName)
    │               ├─ Deny        → POST /deny
    │               └─ Abort       → POST /cancel (stops entire generation)
    │               │
    │               ▼
    │           canUseTool resolves → tool executes or is skipped
    │
    └─ 5-minute timeout → auto-deny
```

### Backend — SessionTrust (permissions.ts)

`SessionTrust` is a `Set<string>` of trusted tool names, scoped to the current conversation. It resets when a new conversation starts (no `sessionId`). The `autoAllowMCPTools` setting in `UniClaudeSettings` bypasses the permission prompt for all tools on both the `uniclaude-unity` and `uniclaude-meta` MCP servers — but not for external plugins.

### Frontend — PermissionPromptElement

An inline overlay in the chat panel that shows the tool name and a human-readable summary of the tool input. Rendered as a VisualElement inside the message flow so the user sees exactly what Claude wants to do before approving.

## Conversation Persistence

Conversations are stored as individual JSON files in `Library/UniClaude/conversations/`.

### Storage Layout

```
Library/UniClaude/
├── conversations/
│   ├── index.json           # ConversationSummary[] (id, title, dates)
│   ├── {uuid-1}.json        # Full Conversation with messages
│   ├── {uuid-2}.json
│   └── ...
├── settings.json            # UniClaudeSettings
└── index.json               # ProjectIndex cache
```

### Data Model

- **Conversation** — `id`, `title`, `createdAt`, `updatedAt`, `sessionId`, `messages[]`
- **ChatMessage** — `role` (User/Assistant/System), `content`, `timestamp`, `activityLog`
- **ActivityLog** — hierarchical record of tool invocations and subagent tasks per message

The `sessionId` is stored so that conversations can be resumed via the Agent SDK's `--resume` flag, maintaining context across multiple chat turns.

### Write Safety

`ConversationStore` uses atomic writes — data is written to a temporary file first, then moved into place. This prevents corruption if Unity crashes or the editor is force-quit mid-write.

### Title Generation

New conversations are titled "New Chat" until the first user message, at which point the title is derived from the message content.

## Activity Tracking

Each assistant message includes an `ActivityLog` that records every tool call and subagent task in a hierarchical structure:

```
AssistantMessage
└── ActivityLog
    ├── ToolActivity (toolUseId, toolName, inputJson)
    ├── ToolActivity (toolUseId, toolName, inputJson)
    └── TaskActivity (taskId, description, status)
        ├── ToolActivity (nested under task)
        └── TaskActivity (child task)
```

This enables audit trails and UI replay of agent decision-making. The log is persisted alongside each message in the conversation JSON.

## Health Check System

The `/healthcheck` slash command runs a diagnostic pipeline that verifies the entire stack is functional:

1. **HealthCheckRunner** orchestrates a sequence of `HealthCheckStep` instances
2. **HealthCheckSteps** provides the built-in checks: Node.js binary reachable, dependencies installed, sidecar process running, MCP server responding, network connectivity
3. Results are displayed inline in the chat with pass/fail status per step

This is used during first-run setup and for troubleshooting connection issues.

## Security

### Path Traversal Protection — PathSandbox

All file operations go through `PathSandbox.Resolve()`, which enforces that paths stay within the project root:

1. Rejects absolute paths and paths starting with `/`, `\`, or drive letters
2. Normalizes backslashes for cross-platform safety
3. Canonicalizes the path (`Path.GetFullPath`) to resolve `..` and `.` segments
4. Verifies the canonical result starts with the project root

`PathSandbox.ResolveWritable()` adds an additional check: writes to `.git/` are blocked.

### Localhost-Only MCP Server

The `HttpTransport` binds to `127.0.0.1` only. The MCP server is not accessible from the network — all communication between the sidecar and MCP server is local HTTP on a port known only to the two processes.

### No Credential Storage

The Anthropic API key is read from the `ANTHROPIC_API_KEY` environment variable by the sidecar at runtime. It is never stored in project files, settings, or preferences.

## Configuration

UniClaude has two configuration stores with different scopes and persistence mechanisms.

### UniClaudeSettings (per-project, JSON)

Stored at `Library/UniClaude/settings.json`. Atomic writes. Loaded via static `UniClaudeSettings.Load()`.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SelectedModel` | string | `null` | Claude model (sonnet / opus / haiku) |
| `SelectedEffort` | string | `"high"` | Reasoning effort (low / medium / high / max) |
| `ChatFontSize` | enum | Medium | UI font size (Small / Medium / Large / ExtraLarge) |
| `ProjectAwarenessEnabled` | bool | `true` | Enable project indexing |
| `PackageIndexOverrides` | dict | empty | Per-package inclusion/exclusion for indexing |
| `ExcludedFolders` | list | empty | Folders to exclude from indexing |
| `SidecarPort` | int | `0` | Sidecar port (0 = auto-assign) |
| `NodePath` | string | `""` | Path to Node.js binary (empty = auto-detect) |
| `VerboseLogging` | bool | `false` | Sidecar log verbosity |
| `ContextTokenBudget` | int | `3300` | Max tokens for project tree summary (0 = unlimited) |
| `AutoAllowMCPTools` | bool | `true` | Auto-approve all UniClaude MCP tools |

### MCPSettings (per-machine, EditorPrefs)

Stored in Unity's `EditorPrefs` so settings survive domain reload immediately (before managed code restarts).

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Port` | int | `0` | MCP server port (0 = auto) |
| `Enabled` | bool | `true` | Server on/off |
| `AutoStart` | bool | `true` | Start server on editor launch |
| `LogLevel` | int | `1` | 0 = None, 1 = Info, 2 = Debug |
| `DomainReloadStrategy` | enum | Auto | Auto or Manual |
| `ReloadTimeoutSeconds` | int | `120` | Safety timeout for auto-unlock |
| `VerboseToolLogging` | bool | `false` | Detailed tool activity in chat |

## Slash Commands

UniClaude supports two kinds of slash commands: local commands handled entirely in the editor, and CLI commands forwarded to Claude as prompts.

### Local Commands

Registered via `SlashCommandRegistry.RegisterLocal(name, description, execute, acceptsArgs)`. Built-in local commands:

- `/clear` — clears chat history
- `/new` — starts a new conversation
- `/settings` — opens the settings panel
- `/healthcheck` — runs the diagnostic pipeline
- `/export` — exports conversation as markdown

### CLI Commands

Discovered automatically from `.md` files in four directories (in priority order):

1. `~/.claude/commands/` — user-level commands
2. `.claude/commands/` — project-level commands
3. `~/.claude/plugins/marketplaces/*/plugins/*/commands/` — marketplace plugin commands
4. `Packages/*/.claude/commands/` — UPM package-level commands

Each `.md` file becomes a command (filename without extension). The description is parsed from YAML frontmatter. Content is read at dispatch time and sent to Claude as a prompt.

Local commands take priority if a CLI command shares the same name.

## Sidecar Internals

### Built-in Tool Filtering

The Agent SDK `tools` option controls which of Claude Code's built-in tools are available. UniClaude removes `Edit` and `Write` from the built-in set so that Claude uses the MCP equivalents (`file_modify_script`, `file_write`, `file_create_script`) instead. This is critical because MCP tool calls flow through `ProcessMainThreadQueue`, which drives domain reload locking and tool-call UI bubbles. Built-in SDK tools bypass the MCP server entirely and would not trigger either mechanism.

As a safety net, `UniClaudeWindow.OnGenerationComplete()` calls `AssetDatabase.Refresh()` unconditionally at the end of every agent turn, catching any edge case where a built-in tool slips through.

### Plugin Discovery (plugins.ts)

The sidecar discovers Claude Code plugins from `~/.claude/plugins/` and the project directory, then passes them to the Agent SDK. This allows Claude to use external plugins alongside UniClaude's built-in MCP tools.

### File Checkpointing and Undo

The Agent SDK is initialized with `enableFileCheckpointing: true`. This allows the sidecar to track file changes made during a conversation turn. The `/undo` endpoint reverts the last set of file changes and returns a summary (files changed, insertions, deletions).

### Attachments

`AttachmentManager` validates and stages file and image attachments before sending. Text files are sent as plain text content. Images are base64-encoded with their media type. The sidecar wraps these into the format expected by the Agent SDK.

### Token Usage

`TokenUsage` is parsed from Agent SDK result events. Each assistant message tracks input tokens, output tokens, and estimated cost in USD. This is displayed in the chat UI alongside the response.

## Key Abstractions

### IAssetScanner

Implement this interface to add support for scanning a new asset type:

```csharp
public interface IAssetScanner
{
    AssetKind Kind { get; }
    bool CanScan(string assetPath);
    IndexEntry Scan(string assetPath);
}
```

Built-in scanners: `ScriptScanner`, `SceneScanner`, `PrefabScanner`, `ShaderScanner`, `ScriptableObjectScanner`, `ProjectSettingsScanner`.

Register custom scanners via `ScannerRegistry.Register()`.

### IIndexRetriever

Implement this interface to change how queries are matched against the index:

```csharp
public interface IIndexRetriever
{
    RetrievalResult Retrieve(string query, ProjectIndex index, RetrievalSettings settings);
}
```

Built-in: `KeywordRetriever` (token-based scoring with dependency walking).

### IDomainReloadStrategy

Implement this interface to customize how the MCP server handles Unity's domain reload during tool execution:

```csharp
public interface IDomainReloadStrategy : IDisposable
{
    void OnToolCallStart(string toolName);
    void OnToolCallEnd(string toolName);
    void OnTurnComplete();
    bool IsLocked { get; }
    event Action<string> OnLog;
}
```

Built-in: `AutoReloadStrategy` (locks assemblies during tool calls, unlocks via `OnTurnComplete()` when the generation finishes and calls `AssetDatabase.Refresh()` to trigger pending recompilation, with 120s safety timeout), `ManualReloadStrategy` (locks assemblies, requires manual unlock).

### IMCPTransport

Implement this interface to change how the MCP server communicates (HTTP, stdio, etc.):

```csharp
public interface IMCPTransport : IDisposable
{
    void Start(int port = 0);
    void Stop();
    bool IsRunning { get; }
    string Endpoint { get; }
    void SetRequestHandler(Func<string, Task<string>> handler);
}
```

Built-in: `HttpTransport` (localhost HTTP listener with JSON-RPC routing).

### [MCPTool] Attribute

Add Unity editor tools by writing a static method:

```csharp
public static class MyTools
{
    [MCPTool("my_tool", "Description of what this tool does")]
    public static MCPToolResult MyTool(
        [MCPToolParam("param1", "What this parameter is for")] string param1)
    {
        // Do something in the Unity editor
        return MCPToolResult.Success("Result text");
    }
}
```

The method is discovered automatically at startup and exposed to Claude via the MCP server.

## Extension Points

| I want to... | Implement / Use |
|--------------|----------------|
| Scan a new asset type | `IAssetScanner` + `ScannerRegistry.Register()` |
| Change query matching | `IIndexRetriever` |
| Add an editor tool for Claude | `[MCPTool]` static method returning `MCPToolResult` |
| Add a slash command | `SlashCommandRegistry` |
| Customize domain reload behavior | `IDomainReloadStrategy` |
