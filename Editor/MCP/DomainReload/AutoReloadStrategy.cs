using System;
using UnityEditor;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Automatically locks domain reload on the first tool call of a turn
    /// and unlocks when the turn completes. Safety timeout auto-unlocks
    /// if OnTurnComplete is never called (e.g. CLI crash).
    /// </summary>
    public class AutoReloadStrategy : IDomainReloadStrategy
    {
        readonly int _timeoutSeconds;
        double _lastToolCallTime;
        bool _locked;
        bool _disposed;

        /// <inheritdoc />
        public bool IsLocked => _locked;

        /// <inheritdoc />
        public event Action<string> OnLog;

        /// <summary>
        /// Creates an AutoReloadStrategy with the specified safety timeout.
        /// </summary>
        /// <param name="timeoutSeconds">Seconds after last tool call before auto-unlock.</param>
        public AutoReloadStrategy(int timeoutSeconds = 120)
        {
            _timeoutSeconds = timeoutSeconds;
            EditorApplication.update += CheckTimeout;
        }

        /// <inheritdoc />
        public void OnToolCallStart(string toolName)
        {
            _lastToolCallTime = EditorApplication.timeSinceStartup;

            if (!_locked)
            {
                _locked = true;
                EditorApplication.LockReloadAssemblies();
                OnLog?.Invoke("Locked domain reload (auto — first tool call of turn)");
            }
        }

        /// <inheritdoc />
        public void OnToolCallEnd(string toolName)
        {
            _lastToolCallTime = EditorApplication.timeSinceStartup;
        }

        /// <inheritdoc />
        public void OnTurnComplete()
        {
            if (_locked)
            {
                _locked = false;
                EditorApplication.UnlockReloadAssemblies();
                OnLog?.Invoke("Unlocked domain reload (turn complete)");
                // Trigger pending compilation — Unity won't auto-compile just from unlocking
                AssetDatabase.Refresh();
            }
        }

        void CheckTimeout()
        {
            if (!_locked || _lastToolCallTime == 0) return;

            if (EditorApplication.timeSinceStartup - _lastToolCallTime > _timeoutSeconds)
            {
                _locked = false;
                _lastToolCallTime = 0;
                EditorApplication.UnlockReloadAssemblies();
                OnLog?.Invoke($"Unlocked domain reload (safety timeout — {_timeoutSeconds}s since last tool call)");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            EditorApplication.update -= CheckTimeout;

            if (_locked)
            {
                _locked = false;
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }
}
