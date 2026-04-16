using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// Holds metadata about a single tool parameter.
    /// </summary>
    public class ParameterRegistration
    {
        /// <summary>Gets or sets the parameter name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the parameter description.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets whether the parameter is required.</summary>
        public bool Required { get; set; }

        /// <summary>Gets or sets the C# type of the parameter.</summary>
        public Type Type { get; set; }

        /// <summary>
        /// Gets the JSON Schema type string for this parameter's C# type.
        /// </summary>
        public string JsonType
        {
            get
            {
                if (Type == typeof(string)) return "string";
                if (Type == typeof(int)) return "integer";
                if (Type == typeof(float) || Type == typeof(double)) return "number";
                if (Type == typeof(bool)) return "boolean";
                return "string";
            }
        }
    }

    /// <summary>
    /// Holds metadata about a registered MCP tool.
    /// </summary>
    public class ToolRegistration
    {
        /// <summary>Gets or sets the tool name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the tool description.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets the method to invoke.</summary>
        public MethodInfo Method { get; set; }

        /// <summary>Gets or sets the instance to invoke the method on (null for static).</summary>
        public object Instance { get; set; }

        /// <summary>Gets or sets the parameter registrations.</summary>
        public ParameterRegistration[] Parameters { get; set; }

        /// <summary>
        /// Generates the JSON Schema for this tool's input parameters.
        /// </summary>
        /// <returns>A JObject representing the input schema.</returns>
        public JObject GetInputSchema()
        {
            var schema = new JObject { ["type"] = "object" };

            if (Parameters.Length == 0) return schema;

            var properties = new JObject();
            var required = new JArray();

            foreach (var param in Parameters)
            {
                properties[param.Name] = new JObject
                {
                    ["type"] = param.JsonType,
                    ["description"] = param.Description
                };

                if (param.Required)
                    required.Add(param.Name);
            }

            schema["properties"] = properties;
            if (required.Count > 0)
                schema["required"] = required;

            return schema;
        }
    }

    /// <summary>
    /// Discovers MCP tools via reflection and dispatches JSON-RPC requests to them.
    /// </summary>
    public class MCPDispatcher
    {
        const string SearchToolName = "search_unity_tools";
        const string CallToolName = "call_unity_tool";
        const string ProjectSearchToolName = "project_search";

        const string SearchToolDescription =
            "Search Unity editor tools by keyword. Available categories: " +
            "Scene (hierarchy, create, save, open), " +
            "Prefabs (create, instantiate, edit, variant), " +
            "Components (add, remove, get/set properties), " +
            "Materials (create, assign, shader properties), " +
            "Assets (find, move, import, info), " +
            "Files (read, write, create/modify scripts), " +
            "Animation (controllers, clips), " +
            "Events (listeners), " +
            "References (set, get, find unset), " +
            "Tags/Layers (create, list), " +
            "Project (tests, console, settings, recompile). " +
            "Returns matching tool names, descriptions, and full parameter schemas.";

        readonly Dictionary<string, ToolRegistration> _tools;

        /// <summary>
        /// Creates a new dispatcher, discovering all [MCPTool] methods in the current AppDomain.
        /// </summary>
        public MCPDispatcher()
        {
            _tools = DiscoverTools();
        }

        /// <summary>
        /// Gets all registered tools.
        /// </summary>
        /// <returns>Read-only list of tool registrations.</returns>
        public IReadOnlyList<ToolRegistration> GetTools()
        {
            return _tools.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Handles a JSON-RPC request string and returns a JSON-RPC response string.
        /// Returns null for notifications (no response expected).
        /// </summary>
        /// <param name="json">The JSON-RPC request string.</param>
        /// <returns>A JSON-RPC response string, or null for notifications.</returns>
        public string HandleRequest(string json)
        {
            JObject request;
            try
            {
                request = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return CreateErrorResponse(null, -32700, "Parse error");
            }

            var id = request["id"];
            var method = request["method"]?.ToString();

            if (string.IsNullOrEmpty(method))
                return CreateErrorResponse(id, -32600, "Invalid Request: missing method");

            switch (method)
            {
                case "initialize":
                    return HandleInitialize(id);
                case "notifications/initialized":
                    return null;
                case "tools/list":
                    return HandleToolsList(id);
                case "tools/call":
                    return HandleToolsCall(id, request["params"] as JObject);
                default:
                    return CreateErrorResponse(id, -32601, $"Method not found: {method}");
            }
        }

        /// <summary>
        /// Calls a specific tool by name with the given arguments.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">The tool arguments as a JObject.</param>
        /// <returns>The tool result.</returns>
        public MCPToolResult CallTool(string name, JObject arguments)
        {
            if (!_tools.TryGetValue(name, out var tool))
                return MCPToolResult.Error($"Unknown tool: {name}");

            try
            {
                var args = BindArguments(tool, arguments);
                var result = tool.Method.Invoke(tool.Instance, args);
                return result as MCPToolResult ?? MCPToolResult.Error("Tool did not return MCPToolResult");
            }
            catch (TargetInvocationException ex)
            {
                return MCPToolResult.Error(ex.InnerException?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                return MCPToolResult.Error(ex.Message);
            }
        }

        string HandleInitialize(JToken id)
        {
            var result = new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject()
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "uniclaude",
                    ["version"] = "0.3.0"
                }
            };
            return CreateSuccessResponse(id, result);
        }

        string HandleToolsList(JToken id)
        {
            var toolsArray = new JArray();

            // 1. search_unity_tools
            toolsArray.Add(new JObject
            {
                ["name"] = SearchToolName,
                ["description"] = SearchToolDescription,
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Keywords to search for (e.g. 'material color', 'prefab create', 'scene hierarchy')"
                        }
                    },
                    ["required"] = new JArray("query")
                }
            });

            // 2. call_unity_tool
            toolsArray.Add(new JObject
            {
                ["name"] = CallToolName,
                ["description"] = "Call a Unity editor tool by name. Use search_unity_tools first to find available tools and their parameter schemas.",
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["tool"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Tool name from search results (e.g. 'material_set_property')"
                        },
                        ["input"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "JSON object of tool parameters matching the tool's inputSchema"
                        }
                    },
                    ["required"] = new JArray("tool", "input")
                }
            });

            // 3. project_search — pulled from discovered tools registry
            if (_tools.TryGetValue(ProjectSearchToolName, out var projectSearch))
            {
                toolsArray.Add(new JObject
                {
                    ["name"] = projectSearch.Name,
                    ["description"] = projectSearch.Description,
                    ["inputSchema"] = projectSearch.GetInputSchema()
                });
            }

            return CreateSuccessResponse(id, new JObject { ["tools"] = toolsArray });
        }

        string HandleToolsCall(JToken id, JObject parameters)
        {
            var toolName = parameters?["name"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
                return CreateErrorResponse(id, -32602, "Missing tool name");

            var arguments = parameters["arguments"] as JObject ?? new JObject();

            MCPToolResult toolResult;
            if (toolName == SearchToolName)
                toolResult = SearchTools(arguments);
            else if (toolName == CallToolName)
                toolResult = HandleCallUnityTool(arguments);
            else if (toolName == ProjectSearchToolName)
                toolResult = CallTool(toolName, arguments);
            else
                toolResult = MCPToolResult.Error(
                    $"Unknown tool: '{toolName}'. Available tools: {SearchToolName}, {CallToolName}, {ProjectSearchToolName}");

            return CreateSuccessResponse(id, toolResult.ToMCPResponse());
        }

        MCPToolResult SearchTools(JObject arguments)
        {
            var query = arguments?["query"]?.ToString();
            if (string.IsNullOrWhiteSpace(query))
                return MCPToolResult.Error("Query must not be empty.");

            var keywords = query.ToLowerInvariant().Split(
                new[] { ' ', ',', '_' }, StringSplitOptions.RemoveEmptyEntries);

            var scored = new List<(ToolRegistration tool, int score)>();

            foreach (var tool in _tools.Values)
            {
                var haystack = (tool.Name + " " + tool.Description).ToLowerInvariant();
                var score = 0;
                foreach (var kw in keywords)
                    if (haystack.Contains(kw))
                        score++;

                if (score > 0)
                    scored.Add((tool, score));
            }

            if (scored.Count == 0)
            {
                var hint = new JObject
                {
                    ["message"] = $"No tools matched '{query}'. Try broader keywords or a different category.",
                    ["categories"] = new JArray(
                        "Scene", "Prefabs", "Components", "Materials", "Assets",
                        "Files", "Animation", "Events", "References", "Tags/Layers", "Project")
                };
                return MCPToolResult.Success(hint.ToString(Formatting.None));
            }

            var results = new JArray();
            foreach (var (tool, _) in scored.OrderByDescending(s => s.score).Take(10))
            {
                results.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = tool.GetInputSchema()
                });
            }

            return MCPToolResult.Success(results.ToString(Formatting.None));
        }

        MCPToolResult HandleCallUnityTool(JObject arguments)
        {
            var toolName = arguments?["tool"]?.ToString();
            if (string.IsNullOrWhiteSpace(toolName))
                return MCPToolResult.Error("Missing required parameter: 'tool'.");

            if (!_tools.ContainsKey(toolName))
                return MCPToolResult.Error(
                    $"Unknown tool: '{toolName}'. Use search_unity_tools to find available tools.");

            var inputToken = arguments["input"];
            JObject input;

            if (inputToken == null || inputToken.Type == JTokenType.Null)
            {
                input = new JObject();
            }
            else if (inputToken.Type == JTokenType.Object)
            {
                input = (JObject)inputToken;
            }
            else
            {
                var inputStr = inputToken.ToString();
                try
                {
                    input = JObject.Parse(inputStr);
                }
                catch (JsonException)
                {
                    return MCPToolResult.Error(
                        $"Invalid JSON in 'input' parameter. Expected a JSON object, got: {inputStr.Substring(0, System.Math.Min(inputStr.Length, 100))}");
                }
            }

            return CallTool(toolName, input);
        }

        object[] BindArguments(ToolRegistration tool, JObject arguments)
        {
            var args = new object[tool.Parameters.Length];
            for (int i = 0; i < tool.Parameters.Length; i++)
            {
                var param = tool.Parameters[i];
                var token = arguments?[param.Name];

                if (token == null || token.Type == JTokenType.Null)
                {
                    if (param.Required)
                        throw new ArgumentException($"Missing required parameter: {param.Name}");
                    args[i] = param.Type.IsValueType ? Activator.CreateInstance(param.Type) : null;
                }
                else
                {
                    args[i] = token.ToObject(param.Type);
                }
            }
            return args;
        }

        Dictionary<string, ToolRegistration> DiscoverTools()
        {
            var tools = new Dictionary<string, ToolRegistration>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var type in types)
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var attr = method.GetCustomAttribute<MCPToolAttribute>();
                        if (attr == null) continue;
                        if (method.ReturnType != typeof(MCPToolResult)) continue;

                        var parameters = method.GetParameters()
                            .Select(p =>
                            {
                                var paramAttr = p.GetCustomAttribute<MCPToolParamAttribute>();
                                return new ParameterRegistration
                                {
                                    Name = p.Name,
                                    Description = paramAttr?.Description ?? p.Name,
                                    Required = paramAttr?.Required ?? false,
                                    Type = p.ParameterType
                                };
                            })
                            .ToArray();

                        if (tools.ContainsKey(attr.Name))
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[UniClaude MCP] Duplicate tool name '{attr.Name}' — " +
                                $"{type.FullName}.{method.Name} overwrites " +
                                $"{tools[attr.Name].Method.DeclaringType.FullName}.{tools[attr.Name].Method.Name}");
                        }

                        tools[attr.Name] = new ToolRegistration
                        {
                            Name = attr.Name,
                            Description = attr.Description,
                            Method = method,
                            Instance = null,
                            Parameters = parameters
                        };
                    }
                }
            }

            return tools;
        }

        string CreateSuccessResponse(JToken id, JObject result)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = result
            }.ToString(Formatting.None);
        }

        string CreateErrorResponse(JToken id, int code, string message)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            }.ToString(Formatting.None);
        }
    }
}
