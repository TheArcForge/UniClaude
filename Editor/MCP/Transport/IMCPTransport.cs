using System;
using System.Threading.Tasks;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Abstraction for MCP transport. Implementations handle raw I/O
    /// (HTTP, stdio, etc.) and surface parsed JSON-RPC requests.
    /// </summary>
    public interface IMCPTransport : IDisposable
    {
        /// <summary>Start listening for incoming requests.</summary>
        /// <param name="port">Port to listen on. 0 = OS-assigned.</param>
        void Start(int port = 0);

        /// <summary>Stop listening and clean up resources.</summary>
        void Stop();

        /// <summary>Whether the transport is currently active.</summary>
        bool IsRunning { get; }

        /// <summary>The endpoint address clients should connect to (e.g. "http://localhost:12345/").</summary>
        string Endpoint { get; }

        /// <summary>
        /// Register the handler that processes incoming JSON-RPC request strings
        /// and returns JSON-RPC response strings. Called on a background thread —
        /// handler is responsible for marshalling to main thread if needed.
        /// </summary>
        /// <param name="handler">The request handler function.</param>
        void SetRequestHandler(Func<string, Task<string>> handler);
    }
}
