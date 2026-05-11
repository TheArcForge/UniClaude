# UniClaude — Technical Overview

![UniClaude](Documentation/UniClaudeGIF.gif)

UniClaude is an AI-powered development environment embedded inside the Unity Editor. It gives developers a conversational interface to Claude that can directly inspect and modify Unity scenes, prefabs, components, materials, animations, and project settings — without leaving the editor.

This document is a high-level walkthrough of the project's scope, architecture, and engineering challenges for anyone evaluating the work.

## What It Does

A dockable editor window where developers chat with Claude, and Claude can take action inside Unity. Not just code generation — direct editor manipulation. Ask Claude to set up a scene, wire up components, create prefab variants, configure animation state machines, or review your architecture, and it does it through 75+ purpose-built tools while you watch and approve each step.

## Scope

UniClaude is a solo-developed, full-stack project spanning two runtimes, two languages, and multiple protocol layers:

| Layer | Technology | Lines |
|-------|-----------|-------|
| Editor UI | C# / Unity UI Toolkit | ~4,000 |
| MCP tool server | C# / JSON-RPC 2.0 | ~5,000 |
| Core services (indexing, lifecycle, persistence) | C# | ~3,000 |
| Node.js sidecar (agent orchestration) | TypeScript / Express | ~1,500 |
| Installer system (Ninja mode) | Node.js + shell | ~1,000 |
| Architecture skills (AI decision frameworks) | Markdown / prompt engineering | ~1,700 |
| Test suites | C# (NUnit) + TypeScript | ~2,000 |

## Architecture

### Two-Process Sidecar Pattern

The Anthropic Agent SDK is a Node.js library. Rather than embedding a JavaScript runtime inside Unity (impractical), UniClaude uses a sidecar pattern: a Node.js process handles all AI communication while the Unity editor stays pure C#.

```
Unity Editor (C#)                     Node.js Sidecar
┌───────────────────────────┐         ┌────────────────────────────┐
│ Editor Window (UI Toolkit)│         │ Express HTTP Server        │
│ SidecarClient ────────────│── HTTP ─│ Anthropic Agent SDK        │
│ MCPServer <───────────────│── SSE ──│ Streams responses back     │
│   75+ tools via JSON-RPC  │── RPC ──│ Calls tools in Unity       │
│ ProjectAwareness (index)  │         │ Permission gating          │
└───────────────────────────┘         └────────────────────────────┘
         localhost only                     auto-assigned port
```

Communication flows:
- **Chat:** HTTP POST from Unity to sidecar, SSE stream back for real-time token streaming
- **Tool calls:** Claude decides to act, Agent SDK sends JSON-RPC to Unity's MCP server, tool executes on Unity's main thread, result flows back
- **Permissions:** Every tool call pauses for user approval via an async callback chain spanning both processes

### Key Engineering Challenges

**Domain reload resilience.** Unity recompiles and reloads all C# assemblies whenever a script changes. This destroys all in-memory state, kills active connections, and nulls every C# reference. UniClaude survives this by caching process state in Unity's `SessionState`, reconnecting SSE streams with `Last-Event-ID` replay, and using a watchdog that detects stream silence and auto-reconnects (up to 3 retries). Tool execution is gated through a domain reload lock that pauses MCP dispatch during recompilation.

**Main-thread dispatch.** Unity's editor APIs are main-thread-only, but MCP requests arrive on HTTP listener threads. Every tool call is enqueued into a thread-safe queue and drained on `EditorApplication.update`. The MCP request blocks (async wait) until the main thread processes it and posts the result back.

**Atomic persistence.** Conversations and settings are written to temp files first, then atomically moved into place. A Unity crash mid-write cannot corrupt saved state.

**SSE reconnection with event replay.** The sidecar buffers all SSE events for the active query. On reconnect (after domain reload or network hiccup), the client sends `Last-Event-ID` and the sidecar replays missed events before resuming the live stream. From the user's perspective, the conversation continues seamlessly.

## MCP Tool System

75+ tools organized into 16 categories, all discovered via reflection at startup. Any static method annotated with `[MCPTool]` is automatically registered and exposed to Claude.

**Categories:** Scene hierarchy, components, prefabs, materials, animation, files, assets, asset import settings, references, inspector, scene management, events, tags/layers, project settings, project search, domain reload control.

**Design principles:**
- Every tool integrates with Unity's Undo system for reversibility
- All file paths validated through a `PathSandbox` that prevents traversal attacks
- Batch operations (`scene_setup`, `component_set_properties`) minimize round trips
- Script editing is wrapped in `BeginScriptEditing`/`EndScriptEditing` to defer recompilation

## Project Awareness

An indexing pipeline that gives Claude context about the project without the developer having to explain it:

1. **Scanners** parse C# scripts, scenes, prefabs, shaders, ScriptableObjects, and project settings into structured index entries
2. **Keyword retriever** scores index entries against the user's query using token-based matching with dependency walking
3. **Context formatter** builds a two-tier system prompt: Tier 1 (always included) is a breadth-first project tree capped by a configurable token budget; Tier 2 (per-message) adds the most relevant files for the current query

## Installer System

UniClaude ships with a Ninja Install Mode that makes it invisible to `git status` on team projects — so individual developers can use AI tooling without affecting the shared repository.

Implementation: a git clean/smudge filter on `packages-lock.json` strips UniClaude entries on commit and restores them on checkout. Local git excludes hide the embedded package folder. Mode conversion (Ninja to Standard and back) is orchestrated through a multi-step flow that survives Unity restarts: a detached Node.js helper polls Unity's PID, performs filesystem operations after quit, and relaunches the editor. A persistent progress window tracks each step across the restart boundary.

## AI Architecture Skills

10 Claude Code skills that turn Claude into an opinionated Unity architecture advisor. Delivered as a local plugin via the Agent SDK's plugin discovery system.

The skills form a layered decision framework: a top-level architect skill with condensed decision trees delegates to specialized skills for deep dives into component design, data modeling, scene architecture, prefab strategy, performance optimization, and code review. Workflow skills provide efficient MCP tool sequences for common authoring tasks.

## Testing

- **C# tests (NUnit):** MCP tool behavior, path sandboxing, context formatting, index operations, health checks
- **TypeScript tests (Node.js built-in runner):** Agent SDK integration, plugin passthrough, permission flow, SSE event handling, tool activity tracking, undo/rewind, task lifecycle

## Technologies

C#, TypeScript, Unity 6 (UI Toolkit), Node.js, Express, Anthropic Agent SDK, Model Context Protocol (MCP), JSON-RPC 2.0, Server-Sent Events (SSE), Git (clean/smudge filters, local excludes)
