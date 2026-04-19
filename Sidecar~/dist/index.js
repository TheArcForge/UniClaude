"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
// src/index.ts
const server_js_1 = require("./server.js");
function parseArgs(args) {
    let port = 0;
    let mcpPort = 0;
    for (let i = 0; i < args.length; i++) {
        if (args[i] === "--port" && args[i + 1]) {
            port = parseInt(args[i + 1], 10);
            i++;
        }
        else if (args[i] === "--mcp-port" && args[i + 1]) {
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
(0, server_js_1.createServer)({ port, mcpPort });
//# sourceMappingURL=index.js.map