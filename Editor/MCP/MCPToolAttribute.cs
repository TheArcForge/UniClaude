using System;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Marks a static method as an MCP tool, making it discoverable by the MCP dispatcher.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MCPToolAttribute : Attribute
    {
        /// <summary>Gets the tool name used in MCP protocol messages.</summary>
        public string Name { get; }

        /// <summary>Gets the human-readable description of what this tool does.</summary>
        public string Description { get; }

        /// <summary>
        /// Creates a new MCP tool attribute.
        /// </summary>
        /// <param name="name">The tool name (e.g. "scene_get_hierarchy").</param>
        /// <param name="description">A description of what the tool does.</param>
        public MCPToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Describes an MCP tool parameter for schema generation and documentation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class MCPToolParamAttribute : Attribute
    {
        /// <summary>Gets the human-readable description of this parameter.</summary>
        public string Description { get; }

        /// <summary>Gets whether this parameter is required.</summary>
        public bool Required { get; }

        /// <summary>
        /// Creates a new MCP tool parameter attribute.
        /// </summary>
        /// <param name="description">A description of the parameter.</param>
        /// <param name="required">Whether the parameter is required (default: false).</param>
        public MCPToolParamAttribute(string description, bool required = false)
        {
            Description = description;
            Required = required;
        }
    }
}
