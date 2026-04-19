// src/server.ts
import express, { Request, Response } from "express";
import { AgentRunner } from "./agent.js";
import type {
  ChatRequest,
  ApproveRequest,
  DenyRequest,
  HealthResponse,
  SSEEvent,
} from "./types.js";

const VERSION = "0.1.0";

export interface ServerOptions {
  port: number;
  mcpPort: number;
}

export function createServer(options: ServerOptions) {
  const app = express();
  app.use(express.json({ limit: "10mb" }));

  // SSE client management with event buffering for reconnect
  let sseClient: Response | null = null;
  let eventSeq = 0;
  let queryEventBuffer: Array<{ id: number; data: string }> = [];

  function emitSSE(event: SSEEvent): void {
    const id = ++eventSeq;
    const data = JSON.stringify(event);
    queryEventBuffer.push({ id, data });

    if (sseClient) {
      sseClient.write(`id: ${id}\ndata: ${data}\n\n`);
    }
    // If no client connected, events are still buffered for replay
  }

  const agent = new AgentRunner({
    mcpPort: options.mcpPort,
    onEvent: emitSSE,
  });

  // Heartbeat tracking
  let lastHealthPing = Date.now();
  const HEARTBEAT_TIMEOUT_MS = 60_000;

  const heartbeatInterval = setInterval(() => {
    if (Date.now() - lastHealthPing > HEARTBEAT_TIMEOUT_MS) {
      console.log("[sidecar] No health ping in 60s — shutting down");
      process.exit(0);
    }
  }, 10_000);

  // ── Routes ──

  app.get("/health", (_req: Request, res: Response) => {
    lastHealthPing = Date.now();
    const response: HealthResponse = {
      status: "ok",
      version: VERSION,
      query_active: agent.isQueryActive,
      trusted_tools: agent.trustedTools,
    };
    res.json(response);
  });

  app.get("/stream", (req: Request, res: Response) => {
    // Close previous SSE client if any
    if (sseClient) {
      sseClient.end();
    }

    res.writeHead(200, {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache",
      Connection: "keep-alive",
    });

    // Replay missed events if Last-Event-ID is provided
    const lastId = parseInt(req.headers["last-event-id"] as string, 10);
    if (!isNaN(lastId)) {
      for (const entry of queryEventBuffer) {
        if (entry.id > lastId) {
          res.write(`id: ${entry.id}\ndata: ${entry.data}\n\n`);
        }
      }
    }

    sseClient = res;

    // SSE keep-alive
    const keepAlive = setInterval(() => {
      res.write(": keepalive\n\n");
    }, 15_000);

    res.on("close", () => {
      clearInterval(keepAlive);
      if (sseClient === res) {
        sseClient = null;
      }
    });
  });

  app.post("/chat", async (req: Request, res: Response) => {
    if (agent.isQueryActive) {
      res.status(409).json({ error: "A query is already active. Cancel first." });
      return;
    }

    const request = req.body as ChatRequest;
    const hasAttachments = request.attachments && request.attachments.length > 0;
    if (!request.message && !hasAttachments) {
      res.status(400).json({ error: "message or attachments required" });
      return;
    }

    // Clear previous query's event buffer
    queryEventBuffer = [];

    // Respond immediately — events flow via SSE
    res.json({ ok: true });

    // Start query in background (events emitted via SSE)
    agent.startQuery(request).catch((err) => {
      emitSSE({ type: "error", message: String(err) });
    });
  });

  app.post("/approve", (req: Request, res: Response) => {
    const { id, type, answer } = req.body as ApproveRequest & { answer?: string };
    if (!id || !type) {
      res.status(400).json({ error: "id and type are required" });
      return;
    }

    const resolved = agent.resolvePermission(id, { type, answer });
    if (!resolved) {
      res.status(404).json({ error: "No pending request with this id" });
      return;
    }

    res.json({ ok: true });
  });

  app.post("/deny", (req: Request, res: Response) => {
    const { id } = req.body as DenyRequest;
    if (!id) {
      res.status(400).json({ error: "id is required" });
      return;
    }

    const resolved = agent.resolvePermission(id, { type: "deny" });
    if (!resolved) {
      res.status(404).json({ error: "No pending request with this id" });
      return;
    }

    res.json({ ok: true });
  });

  app.post("/cancel", (_req: Request, res: Response) => {
    agent.cancelQuery();
    res.json({ ok: true });
  });

  app.post("/undo", async (_req: Request, res: Response) => {
    const result = await agent.undo();
    res.json(result);
  });

  // ── Start ──

  const server = app.listen(options.port, "127.0.0.1", () => {
    const addr = server.address();
    const actualPort =
      typeof addr === "object" && addr ? addr.port : options.port;
    console.log(
      JSON.stringify({ status: "started", port: actualPort, version: VERSION })
    );
  });

  // Cleanup on exit
  process.on("SIGTERM", () => {
    clearInterval(heartbeatInterval);
    server.close();
    process.exit(0);
  });

  process.on("SIGINT", () => {
    clearInterval(heartbeatInterval);
    server.close();
    process.exit(0);
  });

  return server;
}
