// src/types.ts

// ── SSE Event Types ──

export interface TokenEvent {
  type: "token";
  text: string;
}

export interface PhaseEvent {
  type: "phase";
  phase: "thinking" | "writing" | "tool_use";
  tool?: string;
}

export interface PermissionRequestEvent {
  type: "permission_request";
  id: string;
  tool: string;
  input: Record<string, unknown>;
}

export interface ToolExecutedEvent {
  type: "tool_executed";
  tool: string;
  result: string;
  success: boolean;
}

export interface AssistantTextEvent {
  type: "assistant_text";
  text: string;
}

export interface ResultEvent {
  type: "result";
  text: string;
  session_id: string;
  usage: { input: number; output: number };
  cost_usd?: number;
}

export interface ErrorEvent {
  type: "error";
  message: string;
}

export interface PlanModeEvent {
  type: "plan_mode";
  active: boolean;
}

export interface PromptSuggestionEvent {
  type: "prompt_suggestion";
  suggestion: string;
}

export interface ToolActivityEvent {
  type: "tool_activity";
  toolUseId: string;
  toolName: string;
  input: Record<string, unknown>;
  parentTaskId?: string;
}

export interface TaskEvent {
  type: "task";
  taskId: string;
  status: "started" | "progress" | "completed" | "failed" | "stopped";
  description: string;
  error?: string;
}

export interface ToolProgressEvent {
  type: "tool_progress";
  toolUseId: string;
  toolName: string;
  elapsedSeconds: number;
  parentTaskId?: string;
}

export type SSEEvent =
  | TokenEvent
  | PhaseEvent
  | PermissionRequestEvent
  | ToolExecutedEvent
  | AssistantTextEvent
  | ResultEvent
  | ErrorEvent
  | PlanModeEvent
  | PromptSuggestionEvent
  | ToolActivityEvent
  | TaskEvent
  | ToolProgressEvent;

// ── HTTP Request/Response Bodies ──

export interface ChatAttachment {
  type: "text" | "image";
  fileName: string;
  content: string;       // file text or base64 image data
  mediaType?: string;    // e.g. "image/png" — required for images
}

export interface ChatRequest {
  message?: string;
  model?: string;
  effort?: string;
  sessionId?: string;
  systemPrompt?: string;
  autoAllowMCPTools?: boolean;
  projectDir?: string;
  planMode?: boolean;
  attachments?: ChatAttachment[];
}

export interface ApproveRequest {
  id: string;
  type: "allow" | "allow_session";
}

export interface DenyRequest {
  id: string;
}

export interface HealthResponse {
  status: "ok";
  version: string;
  query_active: boolean;
  trusted_tools: string[];
}

// ── Internal Types ──

export interface PermissionDecision {
  type: "allow" | "allow_session" | "deny";
  timeout?: boolean;
  answer?: string;
}
