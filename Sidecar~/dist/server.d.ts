export interface ServerOptions {
    port: number;
    mcpPort: number;
}
export declare function createServer(options: ServerOptions): import("http").Server<typeof import("http").IncomingMessage, typeof import("http").ServerResponse>;
