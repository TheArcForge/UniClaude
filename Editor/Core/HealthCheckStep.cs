using System.Collections.Generic;

namespace UniClaude.Editor
{
    /// <summary>
    /// Definition of a single health check test step.
    /// </summary>
    public class HealthCheckStep
    {
        /// <summary>Human-readable name for this step (e.g. "file_write").</summary>
        public string Name;

        /// <summary>Category for scorecard grouping (e.g. "File operations").</summary>
        public string Category;

        /// <summary>The prompt to send to Claude.</summary>
        public string Prompt;

        /// <summary>The MCP tool name expected in the tool_executed event.</summary>
        public string ExpectedTool;
    }

    /// <summary>
    /// Result of executing a single health check step.
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>The step that was executed.</summary>
        public HealthCheckStep Step;

        /// <summary>Whether the step passed.</summary>
        public bool Passed;

        /// <summary>The tool name that actually fired, or null if none.</summary>
        public string ToolFired;

        /// <summary>Error message if the step failed.</summary>
        public string ErrorMessage;

        /// <summary>How long the step took in seconds.</summary>
        public float Duration;
    }
}
