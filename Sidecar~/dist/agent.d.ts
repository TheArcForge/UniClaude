import type { ChatRequest, ChatAttachment, SSEEvent, PermissionDecision } from "./types.js";
/** Return type from query — extends AsyncIterable and optionally supports rewindFiles. */
export interface QueryLike extends AsyncIterable<Record<string, unknown>> {
    rewindFiles?: (userMessageId: string, options?: {
        dryRun?: boolean;
    }) => Promise<{
        canRewind: boolean;
        error?: string;
        filesChanged?: string[];
        insertions?: number;
        deletions?: number;
    }>;
}
/** Type of the query function accepted by AgentRunner (injectable for testing). */
export type QueryFn = (args: {
    prompt: string | AsyncIterable<Record<string, unknown>>;
    options?: Record<string, unknown>;
}) => QueryLike;
/** Builds a Claude API content block array from message text and attachments.
 *  Returns null when there are no attachments (caller should use plain string prompt). */
export declare function buildContentBlocks(message: string, attachments: ChatAttachment[] | undefined): Array<Record<string, unknown>> | null;
export interface AgentOptions {
    mcpPort: number;
    onEvent: (event: SSEEvent) => void;
    /** Optional query function override — used in tests to capture call arguments. */
    queryFn?: QueryFn;
}
export declare class AgentRunner {
    private _options;
    private _trust;
    private _pendingDecisions;
    private _abortController;
    private _queryActive;
    private _autoAllowMCPTools;
    private _activeQuery;
    private _lastUserMessageId;
    private _pendingToolBlocks;
    private _toolUseToTask;
    constructor(options: AgentOptions);
    get isQueryActive(): boolean;
    get trustedTools(): string[];
    hasPendingDecisions(): boolean;
    getPendingRequests(): Array<{
        id: string;
        tool: string;
    }>;
    startQuery(request: ChatRequest): Promise<void>;
    cancelQuery(): void;
    undo(): Promise<{
        success: boolean;
        message: string;
    }>;
    resolvePermission(id: string, decision: PermissionDecision): boolean;
    private _handleCanUseTool;
    private _handleMessage;
    private _handleStreamEvent;
    private _clearPendingDecisions;
}
