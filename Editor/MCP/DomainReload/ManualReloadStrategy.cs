using System;
using UnityEditor;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Manual domain reload strategy — lock/unlock via explicit
    /// BeginScriptEditing / EndScriptEditing tool calls.
    /// Safety unlock on turn complete if still locked.
    /// </summary>
    public class ManualReloadStrategy : IDomainReloadStrategy
    {
        bool _locked;
        bool _disposed;

        /// <inheritdoc />
        public bool IsLocked => _locked;

        /// <inheritdoc />
        public event Action<string> OnLog;

        /// <summary>Locks domain reload. Called by BeginScriptEditing tool.</summary>
        public void Lock()
        {
            if (!_locked)
            {
                _locked = true;
                EditorApplication.LockReloadAssemblies();
                OnLog?.Invoke("Locked domain reload (manual — BeginScriptEditing)");
            }
        }

        /// <summary>Unlocks domain reload. Called by EndScriptEditing tool.</summary>
        public void Unlock()
        {
            if (_locked)
            {
                _locked = false;
                EditorApplication.UnlockReloadAssemblies();
                OnLog?.Invoke("Unlocked domain reload (manual — EndScriptEditing)");
            }
        }

        /// <inheritdoc />
        public void OnToolCallStart(string toolName) { }

        /// <inheritdoc />
        public void OnToolCallEnd(string toolName) { }

        /// <inheritdoc />
        public void OnTurnComplete()
        {
            if (_locked)
            {
                _locked = false;
                EditorApplication.UnlockReloadAssemblies();
                OnLog?.Invoke("Unlocked domain reload (safety — turn complete with assemblies still locked)");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_locked)
            {
                _locked = false;
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }
}
