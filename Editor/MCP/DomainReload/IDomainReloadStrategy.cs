using System;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Strategy for managing Unity's domain reload when scripts are modified.
    /// </summary>
    public interface IDomainReloadStrategy : IDisposable
    {
        /// <summary>Called when a tool call begins executing.</summary>
        /// <param name="toolName">Name of the tool being called.</param>
        void OnToolCallStart(string toolName);

        /// <summary>Called when a tool call finishes executing.</summary>
        /// <param name="toolName">Name of the tool that finished.</param>
        void OnToolCallEnd(string toolName);

        /// <summary>Called when Claude's entire response turn completes.</summary>
        void OnTurnComplete();

        /// <summary>Whether assemblies are currently locked.</summary>
        bool IsLocked { get; }

        /// <summary>Fired when lock state changes or notable events occur.</summary>
        event Action<string> OnLog;
    }
}
