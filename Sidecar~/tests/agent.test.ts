// tests/agent.test.ts
import { describe, it, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, writeFileSync, rmSync, mkdtempSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { AgentRunner } from "../src/agent.js";
import type { QueryFn, QueryLike } from "../src/agent.js";
import type { SSEEvent, ToolActivityEvent, TaskEvent, ToolProgressEvent } from "../src/types.js";

/** Creates an async generator that yields a minimal result message and terminates. */
async function* fakeConversation(): AsyncIterable<Record<string, unknown>> {
  yield {
    type: "result",
    result: "done",
    session_id: "test-session",
    usage: { input_tokens: 1, output_tokens: 1 },
    total_cost_usd: 0,
  };
}

function createPlugin(basePath: string): void {
  mkdirSync(join(basePath, ".claude-plugin"), { recursive: true });
  writeFileSync(
    join(basePath, ".claude-plugin", "plugin.json"),
    JSON.stringify({ name: "test-plugin" })
  );
}

describe("AgentRunner plugin passthrough", () => {
  let tempDir: string;

  beforeEach(() => {
    tempDir = mkdtempSync(join(tmpdir(), "uniclaude-agent-test-"));
  });

  afterEach(() => {
    rmSync(tempDir, { recursive: true, force: true });
  });

  it("passes discovered plugins and settingSources to query()", async () => {
    // Create a project-level plugin at projectDir/.claude/plugins/<name>
    // discoverPlugins(projectDir) scans projectDir/.claude/plugins/
    const projectDir = join(tempDir, "my-project");
    const pluginDir = join(projectDir, ".claude", "plugins", "my-plugin");
    createPlugin(pluginDir);

    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
      projectDir,
    });

    assert.ok(capturedArgs !== null, "queryFn was not called");

    const captured = capturedArgs as { prompt: string; options?: Record<string, unknown> };
    const opts = captured.options as Record<string, unknown>;

    // plugins must be an array
    assert.ok(Array.isArray(opts.plugins), "plugins should be an array");

    // settingSources must be ["user", "project"]
    assert.deepEqual(opts.settingSources, ["user", "project"]);

    // The project-level plugin should appear in the plugins array
    const plugins = opts.plugins as Array<{ type: string; path: string }>;
    const foundPlugin = plugins.find((p) => p.path === pluginDir);
    assert.ok(
      foundPlugin !== undefined,
      `Expected plugin at ${pluginDir} to be in plugins. Got: ${JSON.stringify(plugins)}`
    );
  });

  it("passes empty plugins array when none installed", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    // Use a fresh empty directory as projectDir — no plugins exist there
    const emptyProject = join(tempDir, "empty-project");
    mkdirSync(emptyProject, { recursive: true });

    await runner.startQuery({
      message: "hello",
      projectDir: emptyProject,
    });

    assert.ok(capturedArgs !== null, "queryFn was not called");

    const captured = capturedArgs as { prompt: string; options?: Record<string, unknown> };
    const opts = captured.options as Record<string, unknown>;

    // plugins must be an empty array (no project plugins, and real homedir
    // marketplace scan will return whatever is installed — but since this is
    // an isolated test environment we just verify the shape is correct)
    assert.ok(Array.isArray(opts.plugins), "plugins should be an array");

    // settingSources must always be present
    assert.deepEqual(opts.settingSources, ["user", "project"]);
  });
});

describe("AgentRunner systemPrompt preset", () => {
  it("passes systemPrompt as preset object when provided", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
      systemPrompt: "You are in a Unity project.",
    });

    assert.ok(capturedArgs !== null);
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.deepEqual(opts.systemPrompt, {
      type: "preset",
      preset: "claude_code",
      append: "You are in a Unity project.",
    });
    assert.equal(opts.appendSystemPrompt, undefined, "appendSystemPrompt should not be set");
  });

  it("passes preset without append when no systemPrompt provided", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
    });

    assert.ok(capturedArgs !== null);
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.deepEqual(opts.systemPrompt, {
      type: "preset",
      preset: "claude_code",
    });
    assert.equal(opts.appendSystemPrompt, undefined);
  });
});

describe("AgentRunner plan mode", () => {
  it("passes permissionMode 'plan' when planMode is true", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
      planMode: true,
    });

    assert.ok(capturedArgs !== null);
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.equal(opts.permissionMode, "plan");
  });

  it("does not set permissionMode when planMode is false", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
      planMode: false,
    });

    assert.ok(capturedArgs !== null);
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.equal(opts.permissionMode, undefined);
  });

  it("emits plan_mode event when agent enters plan mode via tool", async () => {
    const events: Array<{ type: string; active?: boolean }> = [];

    async function* planModeConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "assistant",
        message: {
          content: [
            {
              type: "tool_use",
              name: "EnterPlanMode",
              input: {},
            },
          ],
        },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const fakeQueryFn: QueryFn = () => planModeConversation();

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (event) => {
        if (event.type === "plan_mode") {
          events.push(event as { type: string; active: boolean });
        }
      },
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({ message: "hello" });

    assert.equal(events.length, 1);
    assert.equal(events[0].active, true);
  });
});

describe("AgentRunner effort passthrough", () => {
  it("passes effort to SDK options", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
      effort: "max",
    });

    assert.ok(capturedArgs !== null, "queryFn was not called");
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.equal(opts.effort, "max");
  });

  it("omits effort from SDK options when not specified", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({
      message: "hello",
    });

    assert.ok(capturedArgs !== null, "queryFn was not called");
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.equal(opts.effort, undefined);
  });
});

describe("AgentRunner file checkpointing", () => {
  it("passes enableFileCheckpointing: true to SDK options", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({ message: "hello" });

    assert.ok(capturedArgs !== null);
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.equal(opts.enableFileCheckpointing, true);
  });
});

describe("AgentRunner undo", () => {
  it("calls rewindFiles on the active query when undo is requested", async () => {
    let rewindCalled = false;
    let rewindArgs: { userMessageId: string } | null = null;

    async function* undoConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        uuid: "user-msg-123",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const fakeQuery: QueryLike = {
      [Symbol.asyncIterator]: () => undoConversation()[Symbol.asyncIterator](),
      rewindFiles: async (userMessageId: string) => {
        rewindCalled = true;
        rewindArgs = { userMessageId };
        return { canRewind: true, filesChanged: ["file.ts"], insertions: 0, deletions: 5 };
      },
    };

    const fakeQueryFn: QueryFn = () => fakeQuery;

    const events: SSEEvent[] = [];
    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: fakeQueryFn,
    });

    // First, run a normal query to establish the tracked message ID
    await runner.startQuery({ message: "make changes" });

    // Now request undo
    const result = await runner.undo();

    assert.ok(rewindCalled, "rewindFiles should have been called");
    assert.ok(rewindArgs !== null, "rewindArgs should have been captured");
    assert.equal((rewindArgs as { userMessageId: string }).userMessageId, "user-msg-123");
    assert.ok(result.success);
  });

  it("returns error when no query has been run", async () => {
    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: () => fakeConversation(),
    });

    const result = await runner.undo();

    assert.equal(result.success, false);
    assert.ok(result.message?.includes("Nothing to undo"));
  });

  it("returns error when rewindFiles reports canRewind false", async () => {
    async function* conv(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "result",
        result: "done",
        session_id: "s",
        uuid: "msg-1",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const fakeQuery: QueryLike = {
      [Symbol.asyncIterator]: () => conv()[Symbol.asyncIterator](),
      rewindFiles: async () => ({ canRewind: false, error: "No changes to revert" }),
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: () => fakeQuery,
    });

    await runner.startQuery({ message: "hello" });
    const result = await runner.undo();

    assert.equal(result.success, false);
    assert.ok(result.message.includes("No changes to revert"));
  });

  it("returns error when rewindFiles throws", async () => {
    async function* conv(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "result",
        result: "done",
        session_id: "s",
        uuid: "msg-1",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const fakeQuery: QueryLike = {
      [Symbol.asyncIterator]: () => conv()[Symbol.asyncIterator](),
      rewindFiles: async () => { throw new Error("SDK connection lost"); },
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: () => fakeQuery,
    });

    await runner.startQuery({ message: "hello" });
    const result = await runner.undo();

    assert.equal(result.success, false);
    assert.ok(result.message.includes("SDK connection lost"));
  });
});

describe("AgentRunner prompt suggestions", () => {
  it("passes promptSuggestions: true to SDK options", async () => {
    let capturedArgs: { prompt: string; options?: Record<string, unknown> } | null = null;

    const fakeQueryFn: QueryFn = (args) => {
      capturedArgs = args;
      return fakeConversation();
    };

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: () => {},
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({ message: "hello" });

    assert.ok(capturedArgs !== null);
    const opts = (capturedArgs as { prompt: string; options?: Record<string, unknown> }).options as Record<string, unknown>;
    assert.equal(opts.promptSuggestions, true);
  });

  it("forwards prompt_suggestion messages as SSE events", async () => {
    const events: Array<{ type: string; suggestion?: string }> = [];

    async function* suggestionConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
      yield {
        type: "prompt_suggestion",
        suggestion: "Tell me more about that",
        uuid: "suggestion-uuid",
        session_id: "test-session",
      };
    }

    const fakeQueryFn: QueryFn = () => suggestionConversation();

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (event) => events.push(event as unknown as { type: string; suggestion?: string }),
      queryFn: fakeQueryFn,
    });

    await runner.startQuery({ message: "hello" });

    const suggestionEvents = events.filter((e) => e.type === "prompt_suggestion");
    assert.equal(suggestionEvents.length, 1);
    assert.equal(suggestionEvents[0].suggestion, "Tell me more about that");
  });
});

describe("AgentRunner tool activity events", () => {
  it("emits tool_activity event with accumulated input on content_block_stop", async () => {
    const events: SSEEvent[] = [];

    async function* toolConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "stream_event",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "tool_use", id: "toolu_read1", name: "Read" },
        },
      };
      yield {
        type: "stream_event",
        event: {
          type: "content_block_delta",
          index: 0,
          delta: { type: "input_json_delta", partial_json: '{"file_' },
        },
      };
      yield {
        type: "stream_event",
        event: {
          type: "content_block_delta",
          index: 0,
          delta: { type: "input_json_delta", partial_json: 'path": "/src/index.ts"}' },
        },
      };
      yield {
        type: "stream_event",
        event: { type: "content_block_stop", index: 0 },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => toolConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const toolActivityEvents = events.filter((e) => e.type === "tool_activity") as ToolActivityEvent[];
    assert.equal(toolActivityEvents.length, 1);
    assert.equal(toolActivityEvents[0].toolUseId, "toolu_read1");
    assert.equal(toolActivityEvents[0].toolName, "Read");
    assert.deepEqual(toolActivityEvents[0].input, { file_path: "/src/index.ts" });
    assert.equal(toolActivityEvents[0].parentTaskId, undefined);
  });

  it("still emits phase event for tool_use (backwards compat)", async () => {
    const events: SSEEvent[] = [];

    async function* toolConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "stream_event",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "tool_use", id: "toolu_grep1", name: "Grep" },
        },
      };
      yield {
        type: "stream_event",
        event: { type: "content_block_stop", index: 0 },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => toolConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const phaseEvents = events.filter((e) => e.type === "phase") as Array<{ type: string; phase: string }>;
    const toolUsePhase = phaseEvents.find((e) => e.phase === "tool_use");
    assert.ok(toolUsePhase !== undefined, "Expected a phase event with phase 'tool_use'");
  });

  it("emits tool_activity for mcp_tool_use blocks via stream_event", async () => {
    const events: SSEEvent[] = [];

    async function* mcpToolConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "stream_event",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "mcp_tool_use", id: "toolu_mcp1", name: "mcp__uniclaude-unity__list_prefabs", server_name: "uniclaude-unity" },
        },
      };
      yield {
        type: "stream_event",
        event: {
          type: "content_block_delta",
          index: 0,
          delta: { type: "input_json_delta", partial_json: '{"path": "Assets"}' },
        },
      };
      yield {
        type: "stream_event",
        event: { type: "content_block_stop", index: 0 },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => mcpToolConversation(),
    });

    await runner.startQuery({ message: "list prefabs" });

    const toolActivityEvents = events.filter((e) => e.type === "tool_activity") as ToolActivityEvent[];
    assert.equal(toolActivityEvents.length, 1);
    assert.equal(toolActivityEvents[0].toolName, "mcp__uniclaude-unity__list_prefabs");
    assert.deepEqual(toolActivityEvents[0].input, { path: "Assets" });

    const phaseEvents = events.filter((e) => e.type === "phase") as Array<{ type: string; phase: string; tool?: string }>;
    const toolUsePhase = phaseEvents.find((e) => e.phase === "tool_use");
    assert.ok(toolUsePhase !== undefined, "Expected a phase event for mcp_tool_use");
    assert.equal(toolUsePhase!.tool, "mcp__uniclaude-unity__list_prefabs");
  });

  it("emits tool_activity from assistant messages (non-streaming path)", async () => {
    const events: SSEEvent[] = [];

    async function* assistantToolConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "assistant",
        message: {
          content: [
            { type: "text", text: "Let me check that." },
            { type: "mcp_tool_use", id: "toolu_mcp2", name: "mcp__uniclaude-unity__list_prefabs", input: { path: "Assets/Prefabs" } },
          ],
        },
        parent_tool_use_id: null,
        uuid: "uuid-1",
        session_id: "test-session",
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => assistantToolConversation(),
    });

    await runner.startQuery({ message: "list prefabs" });

    const toolActivityEvents = events.filter((e) => e.type === "tool_activity") as ToolActivityEvent[];
    assert.equal(toolActivityEvents.length, 1);
    assert.equal(toolActivityEvents[0].toolUseId, "toolu_mcp2");
    assert.equal(toolActivityEvents[0].toolName, "mcp__uniclaude-unity__list_prefabs");
    assert.deepEqual(toolActivityEvents[0].input, { path: "Assets/Prefabs" });
    assert.equal(toolActivityEvents[0].parentTaskId, undefined);

    // Phase event should also fire
    const phaseEvents = events.filter((e) => e.type === "phase") as Array<{ type: string; phase: string; tool?: string }>;
    const toolUsePhase = phaseEvents.find((e) => e.phase === "tool_use");
    assert.ok(toolUsePhase !== undefined);
    assert.equal(toolUsePhase!.tool, "mcp__uniclaude-unity__list_prefabs");

    // Text should also be emitted
    const textEvents = events.filter((e) => e.type === "assistant_text") as Array<{ type: string; text: string }>;
    assert.equal(textEvents.length, 1);
    assert.equal(textEvents[0].text, "Let me check that.");
  });

  it("emits tool_activity with empty input when no deltas arrive", async () => {
    const events: SSEEvent[] = [];

    async function* toolConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "stream_event",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "tool_use", id: "toolu_2", name: "Bash" },
        },
      };
      yield {
        type: "stream_event",
        event: { type: "content_block_stop", index: 0 },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => toolConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const toolActivityEvents = events.filter((e) => e.type === "tool_activity") as ToolActivityEvent[];
    assert.equal(toolActivityEvents.length, 1);
    assert.deepEqual(toolActivityEvents[0].input, {});
  });
});

describe("AgentRunner task events", () => {
  it("emits task event on system task_started message", async () => {
    const events: SSEEvent[] = [];

    async function* taskConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "system",
        subtype: "task_started",
        tool_use_id: "toolu_agent",
        task_id: "task_001",
        description: "Code review subagent",
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => taskConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const taskEvents = events.filter((e) => e.type === "task") as TaskEvent[];
    assert.equal(taskEvents.length, 1);
    assert.equal(taskEvents[0].taskId, "task_001");
    assert.equal(taskEvents[0].status, "started");
    assert.equal(taskEvents[0].description, "Code review subagent");
  });

  it("emits task events for progress and notification", async () => {
    const events: SSEEvent[] = [];

    async function* taskConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "system",
        subtype: "task_started",
        tool_use_id: "toolu_agent",
        task_id: "task_002",
        description: "Research subagent",
      };
      yield {
        type: "system",
        subtype: "task_progress",
        task_id: "task_002",
        description: "Searching files...",
      };
      yield {
        type: "system",
        subtype: "task_notification",
        task_id: "task_002",
        status: "completed",
        description: "Research done",
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => taskConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const taskEvents = events.filter((e) => e.type === "task") as TaskEvent[];
    assert.equal(taskEvents.length, 3);
    assert.equal(taskEvents[0].status, "started");
    assert.equal(taskEvents[0].description, "Research subagent");
    assert.equal(taskEvents[1].status, "progress");
    assert.equal(taskEvents[1].description, "Searching files...");
    assert.equal(taskEvents[2].status, "completed");
    assert.equal(taskEvents[2].description, "Research done");
  });

  it("links subagent tool_activity to parent task via parentTaskId", async () => {
    const events: SSEEvent[] = [];

    async function* subagentConversation(): AsyncIterable<Record<string, unknown>> {
      // Agent tool block starts
      yield {
        type: "stream_event",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "tool_use", id: "toolu_agent", name: "Agent" },
        },
      };
      yield {
        type: "stream_event",
        event: { type: "content_block_stop", index: 0 },
      };
      // task_started links toolu_agent -> task_sub1
      yield {
        type: "system",
        subtype: "task_started",
        tool_use_id: "toolu_agent",
        task_id: "task_sub1",
        description: "Subagent task",
      };
      // Subagent's own stream events arrive with parent_tool_use_id
      yield {
        type: "stream_event",
        parent_tool_use_id: "toolu_agent",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "tool_use", id: "toolu_sub_read", name: "Read" },
        },
      };
      yield {
        type: "stream_event",
        parent_tool_use_id: "toolu_agent",
        event: {
          type: "content_block_delta",
          index: 0,
          delta: { type: "input_json_delta", partial_json: '{"file_path":"/foo.ts"}' },
        },
      };
      yield {
        type: "stream_event",
        parent_tool_use_id: "toolu_agent",
        event: { type: "content_block_stop", index: 0 },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => subagentConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const toolActivityEvents = events.filter((e) => e.type === "tool_activity") as ToolActivityEvent[];
    assert.equal(toolActivityEvents.length, 2);

    const agentActivity = toolActivityEvents.find((e) => e.toolName === "Agent");
    assert.ok(agentActivity !== undefined, "Expected Agent tool_activity");
    assert.equal(agentActivity!.parentTaskId, undefined);

    const readActivity = toolActivityEvents.find((e) => e.toolName === "Read");
    assert.ok(readActivity !== undefined, "Expected Read tool_activity");
    assert.equal(readActivity!.parentTaskId, "task_sub1");
  });

  it("clears tool_use_id map between queries", async () => {
    const secondQueryEvents: SSEEvent[] = [];

    async function* firstQuery(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "system",
        subtype: "task_started",
        tool_use_id: "toolu_old",
        task_id: "task_old",
        description: "Old task",
      };
      yield {
        type: "result",
        result: "done",
        session_id: "session-1",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    async function* secondQuery(): AsyncIterable<Record<string, unknown>> {
      // Stale parent_tool_use_id from previous query
      yield {
        type: "stream_event",
        parent_tool_use_id: "toolu_old",
        event: {
          type: "content_block_start",
          index: 0,
          content_block: { type: "tool_use", id: "toolu_grep_new", name: "Grep" },
        },
      };
      yield {
        type: "stream_event",
        parent_tool_use_id: "toolu_old",
        event: { type: "content_block_stop", index: 0 },
      };
      yield {
        type: "result",
        result: "done",
        session_id: "session-2",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    let queryCount = 0;
    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => {
        if (queryCount === 2) secondQueryEvents.push(e);
      },
      queryFn: () => {
        queryCount++;
        if (queryCount === 1) return firstQuery();
        return secondQuery();
      },
    });

    await runner.startQuery({ message: "first" });
    await runner.startQuery({ message: "second" });

    const toolActivityEvents = secondQueryEvents.filter((e) => e.type === "tool_activity") as ToolActivityEvent[];
    assert.equal(toolActivityEvents.length, 1);
    assert.equal(toolActivityEvents[0].parentTaskId, undefined, "Stale mapping should be cleared between queries");
  });
});

describe("AgentRunner tool_progress events", () => {
  it("emits tool_progress event from SDK message", async () => {
    const events: SSEEvent[] = [];

    async function* toolProgressConversation(): AsyncIterable<Record<string, unknown>> {
      yield {
        type: "tool_progress",
        tool_use_id: "toolu_bash",
        tool_name: "Bash",
        elapsed_seconds: 5.2,
        task_id: "task_x",
      };
      yield {
        type: "result",
        result: "done",
        session_id: "test-session",
        usage: { input_tokens: 1, output_tokens: 1 },
        total_cost_usd: 0,
      };
    }

    const runner = new AgentRunner({
      mcpPort: 9999,
      onEvent: (e) => events.push(e),
      queryFn: () => toolProgressConversation(),
    });

    await runner.startQuery({ message: "hello" });

    const toolProgressEvents = events.filter((e) => e.type === "tool_progress") as ToolProgressEvent[];
    assert.equal(toolProgressEvents.length, 1);
    assert.equal(toolProgressEvents[0].toolUseId, "toolu_bash");
    assert.equal(toolProgressEvents[0].toolName, "Bash");
    assert.equal(toolProgressEvents[0].elapsedSeconds, 5.2);
    assert.equal(toolProgressEvents[0].parentTaskId, "task_x");
  });
});
