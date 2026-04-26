// src/agent.ts
import { query, createSdkMcpServer, tool } from "@anthropic-ai/claude-agent-sdk";
import { randomUUID } from "node:crypto";
import { SessionTrust } from "./permissions.js";
import { discoverPlugins } from "./plugins.js";
import type {
  ChatRequest,
  ChatAttachment,
  SSEEvent,
  PermissionDecision,
} from "./types.js";
import type { PermissionUpdate } from "@anthropic-ai/claude-agent-sdk";

/** Return type from query — extends AsyncIterable and optionally supports rewindFiles. */
export interface QueryLike extends AsyncIterable<Record<string, unknown>> {
  rewindFiles?: (userMessageId: string, options?: { dryRun?: boolean }) => Promise<{
    canRewind: boolean;
    error?: string;
    filesChanged?: string[];
    insertions?: number;
    deletions?: number;
  }>;
  setMcpServers?: (servers: Record<string, unknown>) => Promise<{
    added: string[];
    removed: string[];
    errors: Record<string, string>;
  }>;
}

/** Type of the query function accepted by AgentRunner (injectable for testing). */
export type QueryFn = (args: { prompt: string | AsyncIterable<Record<string, unknown>>; options?: Record<string, unknown> }) => QueryLike;

/** Builds a Claude API content block array from message text and attachments.
 *  Returns null when there are no attachments (caller should use plain string prompt). */
export function buildContentBlocks(
  message: string,
  attachments: ChatAttachment[] | undefined
): Array<Record<string, unknown>> | null {
  if (!attachments || attachments.length === 0) return null;

  const blocks: Array<Record<string, unknown>> = [];

  for (const att of attachments) {
    if (att.type === "image") {
      blocks.push({
        type: "image",
        source: {
          type: "base64",
          media_type: att.mediaType ?? "image/png", // validated upstream by C# AttachmentManager
          data: att.content,
        },
      });
    } else {
      blocks.push({
        type: "text",
        text: `[${att.fileName}]\n\`\`\`\n${att.content}\n\`\`\``,
      });
    }
  }

  // User's typed message comes last (if non-empty)
  if (message && message.trim().length > 0) {
    blocks.push({ type: "text", text: message });
  }

  return blocks;
}


export interface AgentOptions {
  mcpPort: number;
  onEvent: (event: SSEEvent) => void;
  /** Optional query function override — used in tests to capture call arguments. */
  queryFn?: QueryFn;
}

interface PendingDecision {
  resolve: (decision: PermissionDecision) => void;
  timer: ReturnType<typeof setTimeout>;
  tool: string;
  input: Record<string, unknown>;
}

const PERMISSION_TIMEOUT_MS = 300_000; // 5 minutes

export class AgentRunner {
  private _options: AgentOptions;
  private _trust: SessionTrust;
  private _pendingDecisions: Map<string, PendingDecision> = new Map();
  private _abortController: AbortController | null = null;
  private _queryActive: boolean = false;
  private _autoAllowMCPTools: boolean = false;
  private _activeQuery: QueryLike | null = null;
  private _unityToolsEnabled: boolean = false;
  private _lastUserMessageId: string | null = null;
  private _pendingToolBlocks: Map<string, { id: string; name: string; inputJson: string }> = new Map();
  private _toolUseToTask: Map<string, string> = new Map();

  constructor(options: AgentOptions) {
    this._options = options;
    this._trust = new SessionTrust();
  }

  get isQueryActive(): boolean {
    return this._queryActive;
  }

  get trustedTools(): string[] {
    return this._trust.list();
  }

  hasPendingDecisions(): boolean {
    return this._pendingDecisions.size > 0;
  }

  getPendingRequests(): Array<{ id: string; tool: string }> {
    return [...this._pendingDecisions.keys()].map((id) => ({ id, tool: id }));
  }

  async startQuery(request: ChatRequest): Promise<void> {
    if (this._queryActive) {
      throw new Error("A query is already active");
    }

    // Reset trust on new conversation (no sessionId = new conversation)
    if (!request.sessionId) {
      this._trust.reset();
    }

    this._lastUserMessageId = null;
    this._pendingToolBlocks.clear();
    this._toolUseToTask.clear();
    this._unityToolsEnabled = false;
    this._autoAllowMCPTools = request.autoAllowMCPTools ?? false;

    this._queryActive = true;
    this._abortController = new AbortController();

    // When using AsyncIterable<SDKUserMessage> as the prompt, the generator must
    // stay open until the query completes — closing it early severs the callback
    // channel the SDK uses for canUseTool, hooks, etc.
    // See: https://github.com/anthropics/claude-code/issues/9705
    const promptStreamHandle: { resolve: (() => void) | null } = { resolve: null };

    try {
      const extraArgs: Record<string, string | null> = {};

      const plugins = discoverPlugins(request.projectDir);

      const queryFn = (this._options.queryFn ?? query) as QueryFn;

      const metaServer = createSdkMcpServer({
        name: "uniclaude-meta",
        tools: [
          tool("enable_unity_tools",
            [
              "Enable Unity editor tools for direct manipulation of the Unity project.",
              "Call this ONLY when you need to perform an action in the Unity editor:",
              "- Create/modify/delete GameObjects, scenes, or prefabs",
              "- Add/remove/configure components and their properties",
              "- Create/edit assets, materials, or animations",
              "- Read/write/delete project files on disk",
              "- Run tests or read console output",
              "",
              "Do NOT call this tool for:",
              "- Explaining Unity concepts, APIs, or best practices",
              "- Writing or reviewing C# code or scripts",
              "- Answering questions about how Unity features work",
              "- Designing systems, architectures, or game logic",
              "- Any request that can be answered with knowledge alone",
            ].join("\n"),
            {},
            async () => {
              if (this._unityToolsEnabled) {
                return { content: [{ type: "text" as const, text: "Unity tools are already enabled." }] };
              }

              if (!this._activeQuery?.setMcpServers) {
                return { content: [{ type: "text" as const, text: "No active session." }], isError: true };
              }

              const result = await this._activeQuery.setMcpServers({
                "uniclaude-unity": {
                  type: "http" as const,
                  url: `http://127.0.0.1:${this._options.mcpPort}/rpc`,
                },
              });

              if (result.errors["uniclaude-unity"]) {
                return {
                  content: [{ type: "text" as const, text: `Failed to connect Unity MCP server: ${result.errors["uniclaude-unity"]}` }],
                  isError: true,
                };
              }

              this._unityToolsEnabled = true;
              return { content: [{ type: "text" as const, text: "Unity editor tools enabled. You now have access to scene, asset, component, prefab, material, and file tools." }] };
            },
          ),
        ],
      });

      const prompt = request.message ?? "";
      const contentBlocks = buildContentBlocks(prompt, request.attachments);

      let promptArg: string | AsyncIterable<Record<string, unknown>>;

      if (contentBlocks) {
        const streamDone = new Promise<void>(resolve => { promptStreamHandle.resolve = resolve; });
        const userMessage = {
          type: "user" as const,
          message: { role: "user" as const, content: contentBlocks },
          parent_tool_use_id: null,
        };
        promptArg = (async function* () {
          yield userMessage;
          await streamDone;
        })();
      } else {
        promptArg = prompt;
      }

      const conversation = queryFn({
        prompt: promptArg,
        options: {
          enableFileCheckpointing: true,
          // Remove Edit and Write from built-in tools — MCP equivalents
          // (file_modify_script, file_write, file_create_script) handle these
          // through the Unity MCP server, which enables domain reload tracking
          // and tool-call UI bubbles.
          tools: [
            "Read", "Bash", "Grep", "Glob", "Agent",
            "TodoRead", "TodoWrite",
            "TaskCreate", "TaskUpdate", "TaskGet", "TaskList", "TaskOutput", "TaskStop",
            "NotebookEdit", "WebFetch", "WebSearch",
          ],
          model: request.model,
          effort: request.effort,
          permissionMode: request.planMode ? ("plan" as const) : undefined,
          resume: request.sessionId,
          systemPrompt: request.systemPrompt
            ? { type: "preset" as const, preset: "claude_code" as const, append: request.systemPrompt }
            : { type: "preset" as const, preset: "claude_code" as const },
          plugins,
          settingSources: ["user", "project"],
          mcpServers: {
            "uniclaude-meta": metaServer,
          },
          promptSuggestions: true,
          extraArgs,
          abortController: this._abortController,
          canUseTool: async (tool: string, input: Record<string, unknown>, options: { suggestions?: PermissionUpdate[] }) => {
            return this._handleCanUseTool(tool, input, options?.suggestions);
          },
          stderr: (data: string) => {
            console.error("[sdk-stderr]", data);
          },
        },
      });
      this._activeQuery = conversation;

      // Iterate the async generator — each yielded value is an SDKMessage.
      // Release the prompt stream on "result" so the SDK can finish cleanly
      // (resolving in finally would deadlock — SDK waits for stream, we wait for SDK).
      for await (const message of conversation) {
        this._handleMessage(message);
        if ((message as Record<string, unknown>).type === "result") {
          promptStreamHandle.resolve?.();
        }
      }
    } catch (err: unknown) {
      if (this._abortController?.signal.aborted) {
        // Cancelled by user — not an error
        return;
      }
      const message = err instanceof Error ? `${err.message}\n${err.stack}` : String(err);
      console.error("[agent] Query failed:", message);
      this._options.onEvent({ type: "error", message: err instanceof Error ? err.message : String(err) });
    } finally {
      // Release the prompt stream so the async generator can complete
      promptStreamHandle.resolve?.();
      this._queryActive = false;
      this._abortController = null;
      this._clearPendingDecisions();
    }
  }

  cancelQuery(): void {
    this._abortController?.abort();
    this._clearPendingDecisions();
  }

  async undo(): Promise<{ success: boolean; message: string }> {
    if (this._queryActive) {
      return { success: false, message: "Cannot undo while a query is active" };
    }

    if (!this._activeQuery || !this._lastUserMessageId) {
      return { success: false, message: "Nothing to undo" };
    }

    if (!this._activeQuery.rewindFiles) {
      return { success: false, message: "File checkpointing not supported by this query" };
    }

    try {
      const result = await this._activeQuery.rewindFiles(this._lastUserMessageId);
      if (!result.canRewind) {
        return { success: false, message: result.error ?? "Cannot rewind" };
      }
      const fileCount = result.filesChanged?.length ?? 0;
      return {
        success: true,
        message: `Reverted ${fileCount} file${fileCount !== 1 ? "s" : ""}`,
      };
    } catch (err) {
      return {
        success: false,
        message: err instanceof Error ? err.message : String(err),
      };
    }
  }

  resolvePermission(id: string, decision: PermissionDecision): boolean {
    const pending = this._pendingDecisions.get(id);
    if (!pending) return false;

    clearTimeout(pending.timer);
    this._pendingDecisions.delete(id);
    pending.resolve(decision);
    return true;
  }

  private async _handleCanUseTool(
    tool: string,
    input: Record<string, unknown>,
    suggestions?: PermissionUpdate[]
  ): Promise<{ behavior: "allow"; updatedInput: Record<string, unknown>; updatedPermissions?: PermissionUpdate[] } | { behavior: "deny"; message: string; interrupt?: boolean }> {
    // Auto-allow UniClaude MCP tools when the setting is enabled
    if (this._autoAllowMCPTools &&
        (tool.startsWith("mcp__uniclaude-unity__") || tool.startsWith("mcp__uniclaude-meta__"))) {
      return { behavior: "allow", updatedInput: input };
    }

    // Auto-allow internal/non-destructive tools that don't need user permission
    const autoAllowTools = [
      "EnterPlanMode", "ExitPlanMode",
      "TodoWrite", "TodoRead",
      "TaskCreate", "TaskUpdate", "TaskGet", "TaskList", "TaskOutput", "TaskStop",
    ];
    if (autoAllowTools.includes(tool)) {
      return { behavior: "allow", updatedInput: input };
    }

    // Check session trust
    if (this._trust.isTrusted(tool)) {
      return { behavior: "allow", updatedInput: input, updatedPermissions: suggestions };
    }

    // Emit permission request
    const reqId = randomUUID();
    this._options.onEvent({
      type: "permission_request",
      id: reqId,
      tool,
      input,
    });

    // Wait for Unity to respond
    const decision = await new Promise<PermissionDecision>((resolve) => {
      const timer = setTimeout(() => {
        this._pendingDecisions.delete(reqId);
        this._options.onEvent({
          type: "error",
          message: `Permission request for ${tool} timed out after 5 minutes`,
        });
        resolve({ type: "deny", timeout: true });
      }, PERMISSION_TIMEOUT_MS);

      this._pendingDecisions.set(reqId, { resolve, timer, tool, input });
    });

    // Update trust if "allow_session"
    if (decision.type === "allow_session") {
      this._trust.add(tool);
    }

    if (decision.type === "deny") {
      return { behavior: "deny", message: "User denied this tool use." };
    }

    // For AskUserQuestion, inject the user's answer into the input
    if (tool === "AskUserQuestion" && decision.answer) {
      return { behavior: "allow", updatedInput: { ...input, answer: decision.answer } };
    }

    // When user chose "always allow", forward the SDK's permission suggestions
    // so the CLI's own permission engine also records the session-level rule.
    const updatedPermissions = decision.type === "allow_session" ? suggestions : undefined;
    return { behavior: "allow", updatedInput: input, updatedPermissions };
  }

  private _handleMessage(message: Record<string, unknown>): void {
    const type = message.type as string;
    if (type === "result") {
      const msg = message as Record<string, unknown>;
      // Track the result UUID for potential undo
      if (typeof msg.uuid === "string") {
        this._lastUserMessageId = msg.uuid;
      }
      const usage = msg.usage as Record<string, number> | undefined;
      this._options.onEvent({
        type: "result",
        text: typeof msg.result === "string" ? msg.result : "",
        session_id: (msg.session_id as string) ?? "",
        usage: {
          input: usage?.input_tokens ?? 0,
          output: usage?.output_tokens ?? 0,
        },
        cost_usd: msg.total_cost_usd as number | undefined,
      });
      return;
    }

    if (type === "stream_event") {
      const event = message.event as Record<string, unknown> | undefined;
      if (!event) return;
      const parentToolUseId = message.parent_tool_use_id as string | undefined;
      this._handleStreamEvent(event, parentToolUseId);
      return;
    }

    if (type === "assistant") {
      const msg = message as {
        message?: { content?: Array<{ type: string; text?: string; name?: string; id?: string; input?: Record<string, unknown> }> };
        parent_tool_use_id?: string | null;
      };

      const parentToolUseId = msg.parent_tool_use_id ?? undefined;
      const content = msg.message?.content;

      if (content) {
        for (const block of content) {
          // Emit tool activity for all tool use block types
          if (typeof block.type === "string" && block.type.endsWith("tool_use") && block.id && block.name) {
            const input = block.input ?? {};

            // Emit phase event (backwards compat)
            this._options.onEvent({
              type: "phase",
              phase: "tool_use",
              tool: block.name,
            });

            // Derive parentTaskId from the linking map
            const parentTaskId = parentToolUseId
              ? this._toolUseToTask.get(parentToolUseId)
              : undefined;

            this._options.onEvent({
              type: "tool_activity",
              toolUseId: block.id,
              toolName: block.name,
              input,
              parentTaskId,
            });
          }

          // Detect plan mode tool use
          if (block.name === "EnterPlanMode") {
            this._options.onEvent({ type: "plan_mode", active: true });
          } else if (block.name === "ExitPlanMode") {
            this._options.onEvent({ type: "plan_mode", active: false });
          }
        }

        const textBlocks = content.filter(
          (b) => b.type === "text" && b.text
        );
        if (textBlocks.length > 0) {
          const combined = textBlocks.map((b) => b.text!).join("\n\n");
          this._options.onEvent({ type: "assistant_text", text: combined });
        }
      }
      return;
    }

    if (type === "prompt_suggestion") {
      const suggestion = (message as { suggestion?: string }).suggestion;
      if (suggestion) {
        this._options.onEvent({ type: "prompt_suggestion", suggestion });
      }
      return;
    }

    if (type === "system") {
      const subtype = message.subtype as string;
      if (subtype === "task_started") {
        const toolUseId = message.tool_use_id as string;
        const taskId = message.task_id as string;
        const description = (message.description as string) ?? "";
        if (toolUseId && taskId) {
          this._toolUseToTask.set(toolUseId, taskId);
        }
        this._options.onEvent({ type: "task", taskId, status: "started", description });
      } else if (subtype === "task_progress") {
        this._options.onEvent({
          type: "task",
          taskId: message.task_id as string,
          status: "progress",
          description: (message.description as string) ?? "",
        });
      } else if (subtype === "task_notification") {
        this._options.onEvent({
          type: "task",
          taskId: message.task_id as string,
          status: ((message.status as string) ?? "completed") as "started" | "progress" | "completed" | "failed" | "stopped",
          description: (message.description as string) ?? "",
          error: message.error as string | undefined,
        });
      }
      return;
    }

    if (type === "tool_progress") {
      this._options.onEvent({
        type: "tool_progress",
        toolUseId: message.tool_use_id as string,
        toolName: (message.tool_name as string) ?? "",
        elapsedSeconds: (message.elapsed_seconds as number) ?? 0,
        parentTaskId: message.task_id as string | undefined,
      });
      return;
    }
  }

  private _handleStreamEvent(event: Record<string, unknown>, parentToolUseId?: string): void {
    const blockIndex = event.index as number | undefined;
    const blockKey = `${parentToolUseId ?? ""}:${blockIndex ?? 0}`;

    if (event.type === "content_block_start") {
      const block = event.content_block as Record<string, unknown> | undefined;
      if (block) {
        if (block.type === "thinking") {
          this._options.onEvent({ type: "phase", phase: "thinking" });
        } else if (block.type === "text") {
          this._options.onEvent({ type: "phase", phase: "writing" });
        } else if (typeof block.type === "string" && (block.type as string).endsWith("tool_use")) {
          this._options.onEvent({
            type: "phase",
            phase: "tool_use",
            tool: (block.name as string) ?? undefined,
          });
          this._pendingToolBlocks.set(blockKey, {
            id: block.id as string,
            name: block.name as string,
            inputJson: "",
          });
        }
      }
    }

    if (event.type === "content_block_delta") {
      const delta = event.delta as Record<string, unknown> | undefined;
      if (delta?.type === "text_delta" && typeof delta.text === "string") {
        this._options.onEvent({ type: "token", text: delta.text });
      } else if (delta?.type === "input_json_delta" && typeof delta.partial_json === "string") {
        const pending = this._pendingToolBlocks.get(blockKey);
        if (pending) {
          pending.inputJson += delta.partial_json;
        }
      }
    }

    if (event.type === "content_block_stop") {
      const pending = this._pendingToolBlocks.get(blockKey);
      if (pending) {
        let input: Record<string, unknown> = {};
        if (pending.inputJson) {
          try { input = JSON.parse(pending.inputJson); } catch { /* use empty */ }
        }

        const parentTaskId = parentToolUseId
          ? this._toolUseToTask.get(parentToolUseId)
          : undefined;
        this._options.onEvent({
          type: "tool_activity",
          toolUseId: pending.id,
          toolName: pending.name,
          input,
          parentTaskId,
        });
        this._pendingToolBlocks.delete(blockKey);
      }
    }
  }

  private _clearPendingDecisions(): void {
    for (const [, pending] of this._pendingDecisions) {
      clearTimeout(pending.timer);
      pending.resolve({ type: "deny", timeout: true });
    }
    this._pendingDecisions.clear();
  }
}
