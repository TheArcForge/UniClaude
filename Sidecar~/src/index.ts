// src/index.ts
import { createServer } from "./server.js";

function parseArgs(args: string[]): { port: number; mcpPort: number } {
  let port = 0;
  let mcpPort = 0;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--port" && args[i + 1]) {
      port = parseInt(args[i + 1], 10);
      i++;
    } else if (args[i] === "--mcp-port" && args[i + 1]) {
      mcpPort = parseInt(args[i + 1], 10);
      i++;
    }
  }

  if (!mcpPort) {
    console.error("Error: --mcp-port is required");
    process.exit(1);
  }

  return { port, mcpPort };
}

const { port, mcpPort } = parseArgs(process.argv.slice(2));
createServer({ port, mcpPort });
