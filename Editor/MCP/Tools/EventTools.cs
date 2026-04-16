using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace UniClaude.Editor.MCP
{
    /// <summary>
    /// MCP tools for adding, removing, listing, and discovering UnityEvent persistent listeners.
    /// </summary>
    public static class EventTools
    {
        /// <summary>
        /// Adds a persistent listener to a UnityEvent field on a component.
        /// </summary>
        /// <param name="gameObjectPath">GameObject with the event source component.</param>
        /// <param name="componentType">Component type containing the event.</param>
        /// <param name="eventName">Event field name (e.g. 'm_OnClick').</param>
        /// <param name="targetPath">Target GameObject path.</param>
        /// <param name="targetMethod">Method name to call on target.</param>
        /// <param name="argument">Optional argument value for typed listeners.</param>
        /// <param name="argumentType">Optional argument type override.</param>
        /// <returns>Confirmation with listener count, or a contextual error.</returns>
        [MCPTool("event_add_listener", "Add a persistent listener to a UnityEvent (e.g. Button.onClick). " +
            "Supports void methods and methods with a single argument (string, int, float, bool).")]
        public static MCPToolResult AddListener(
            [MCPToolParam("GameObject with the event source component", required: true)] string gameObjectPath,
            [MCPToolParam("Component type containing the event (e.g. 'Button')", required: true)] string componentType,
            [MCPToolParam("Event field name (e.g. 'm_OnClick')", required: true)] string eventName,
            [MCPToolParam("Target GameObject path (the object with the method to call)", required: true)] string targetPath,
            [MCPToolParam("Method name to call on target", required: true)] string targetMethod,
            [MCPToolParam("Argument value (omit for void methods)")] string argument = null,
            [MCPToolParam("Argument type: string, int, float, bool (auto-detected if omitted)")] string argumentType = null)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = ComponentTools.FindComponentType(componentType);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{componentType}'.");

            var component = go.GetComponent(type);
            if (component == null)
                return MCPToolResult.Error($"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'.");

            var eventField = FindUnityEventField(type, eventName);
            if (eventField == null)
            {
                var available = ListUnityEventFields(type);
                return MCPToolResult.Error(
                    $"UnityEvent field '{eventName}' not found on {componentType}. " +
                    $"Available events: {string.Join(", ", available)}");
            }

            var unityEvent = eventField.GetValue(component) as UnityEventBase;
            if (unityEvent == null)
                return MCPToolResult.Error($"Could not access event '{eventName}' on {componentType}.");

            var targetGO = GameObjectResolver.FindByPath(targetPath);
            if (targetGO == null)
                return GameObjectNotFoundError(targetPath);

            var (targetComp, methodInfo) = FindMethod(targetGO, targetMethod, argument != null);
            if (targetComp == null || methodInfo == null)
            {
                var available = ListAvailableMethods(targetGO);
                return MCPToolResult.Error(
                    $"Method '{targetMethod}' not found on any component of '{targetPath}'. " +
                    $"Available methods: {string.Join(", ", available)}");
            }

            Undo.RecordObject(component, $"MCP Add Listener {eventName}");

            if (argument == null)
            {
                UnityEventTools.AddVoidPersistentListener(
                    unityEvent, methodInfo.CreateDelegate(typeof(UnityAction), targetComp) as UnityAction);
            }
            else
            {
                AddTypedListener(unityEvent, targetComp, targetMethod, argument, argumentType);
            }

            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                eventField = eventName,
                target = ComponentTools.GetPath(targetGO),
                method = targetMethod,
                argument = argument,
                listenerCount = unityEvent.GetPersistentEventCount()
            });
        }

        /// <summary>
        /// Removes a persistent listener by index from a UnityEvent.
        /// </summary>
        /// <param name="gameObjectPath">GameObject with the event source component.</param>
        /// <param name="componentType">Component type containing the event.</param>
        /// <param name="eventName">Event field name.</param>
        /// <param name="index">Listener index to remove (0-based).</param>
        /// <returns>Confirmation with remaining count, or error if index invalid.</returns>
        [MCPTool("event_remove_listener", "Remove a persistent listener by index from a UnityEvent")]
        public static MCPToolResult RemoveListener(
            [MCPToolParam("GameObject with the event source component", required: true)] string gameObjectPath,
            [MCPToolParam("Component type containing the event", required: true)] string componentType,
            [MCPToolParam("Event field name", required: true)] string eventName,
            [MCPToolParam("Listener index to remove (0-based)", required: true)] string index)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = ComponentTools.FindComponentType(componentType);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{componentType}'.");

            var component = go.GetComponent(type);
            if (component == null)
                return MCPToolResult.Error($"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'.");

            var eventField = FindUnityEventField(type, eventName);
            if (eventField == null)
                return MCPToolResult.Error($"UnityEvent field '{eventName}' not found on {componentType}.");

            var unityEvent = eventField.GetValue(component) as UnityEventBase;
            if (unityEvent == null)
                return MCPToolResult.Error($"Could not access event '{eventName}'.");

            if (!int.TryParse(index, out var idx))
                return MCPToolResult.Error($"Invalid index: '{index}'. Must be an integer.");

            if (idx < 0 || idx >= unityEvent.GetPersistentEventCount())
                return MCPToolResult.Error(
                    $"Index {idx} out of range. Event has {unityEvent.GetPersistentEventCount()} listener(s).");

            Undo.RecordObject(component, $"MCP Remove Listener {eventName}[{idx}]");
            UnityEventTools.RemovePersistentListener(unityEvent, idx);

            return MCPToolResult.Success(new
            {
                removed = idx,
                remainingListeners = unityEvent.GetPersistentEventCount()
            });
        }

        /// <summary>
        /// Lists all persistent listeners on a UnityEvent.
        /// </summary>
        /// <param name="gameObjectPath">GameObject with the event source component.</param>
        /// <param name="componentType">Component type containing the event.</param>
        /// <param name="eventName">Event field name.</param>
        /// <returns>List of listeners with target, method, and call state.</returns>
        [MCPTool("event_list_listeners", "List all persistent listeners on a UnityEvent field")]
        public static MCPToolResult ListListeners(
            [MCPToolParam("GameObject with the event source component", required: true)] string gameObjectPath,
            [MCPToolParam("Component type containing the event", required: true)] string componentType,
            [MCPToolParam("Event field name", required: true)] string eventName)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var type = ComponentTools.FindComponentType(componentType);
            if (type == null)
                return MCPToolResult.Error($"Component type not found: '{componentType}'.");

            var component = go.GetComponent(type);
            if (component == null)
                return MCPToolResult.Error($"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'.");

            var eventField = FindUnityEventField(type, eventName);
            if (eventField == null)
                return MCPToolResult.Error($"UnityEvent field '{eventName}' not found on {componentType}.");

            var unityEvent = eventField.GetValue(component) as UnityEventBase;
            if (unityEvent == null)
                return MCPToolResult.Error($"Could not access event '{eventName}'.");

            var listeners = new List<object>();
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                var target = unityEvent.GetPersistentTarget(i);
                listeners.Add(new
                {
                    index = i,
                    target = target != null ? target.name : "null",
                    targetType = target != null ? target.GetType().Name : "null",
                    method = unityEvent.GetPersistentMethodName(i),
                    callState = unityEvent.GetPersistentListenerState(i).ToString()
                });
            }

            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                eventField = eventName,
                listeners,
                count = listeners.Count
            });
        }

        /// <summary>
        /// Discovers all UnityEvent fields on a GameObject's components.
        /// </summary>
        /// <param name="gameObjectPath">Name or hierarchy path of the GameObject.</param>
        /// <param name="componentType">Optional component type to scan.</param>
        /// <returns>List of events with field names, types, and listener counts.</returns>
        [MCPTool("event_find_all", "Find all UnityEvent fields on a GameObject's components")]
        public static MCPToolResult FindAllEvents(
            [MCPToolParam("GameObject name or hierarchy path", required: true)] string gameObjectPath,
            [MCPToolParam("Component type to scan (omit to scan all)")] string componentType = null)
        {
            var go = ComponentTools.FindGameObject(gameObjectPath);
            if (go == null)
                return GameObjectNotFoundError(gameObjectPath);

            var components = new List<Component>();
            if (!string.IsNullOrEmpty(componentType))
            {
                var type = ComponentTools.FindComponentType(componentType);
                if (type == null)
                    return MCPToolResult.Error($"Component type not found: '{componentType}'.");
                var comp = go.GetComponent(type);
                if (comp == null)
                    return MCPToolResult.Error($"Component '{componentType}' not found on '{ComponentTools.GetPath(go)}'.");
                components.Add(comp);
            }
            else
            {
                components.AddRange(go.GetComponents<Component>().Where(c => c != null));
            }

            var events = new List<object>();
            foreach (var comp in components)
            {
                var fields = comp.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    if (typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                    {
                        var evt = field.GetValue(comp) as UnityEventBase;
                        events.Add(new
                        {
                            component = comp.GetType().Name,
                            field = field.Name,
                            eventType = field.FieldType.Name,
                            listenerCount = evt?.GetPersistentEventCount() ?? 0
                        });
                    }
                }
            }

            return MCPToolResult.Success(new
            {
                gameObject = ComponentTools.GetPath(go),
                events,
                count = events.Count
            });
        }

        // ── Helpers ──

        static MCPToolResult GameObjectNotFoundError(string path)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(r => r.name).ToArray();
            return MCPToolResult.Error(
                $"GameObject not found: '{path}'. Root objects in scene: {string.Join(", ", roots)}");
        }

        static FieldInfo FindUnityEventField(Type componentType, string fieldName)
        {
            var field = componentType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                return field;
            return null;
        }

        static string[] ListUnityEventFields(Type componentType)
        {
            return componentType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => typeof(UnityEventBase).IsAssignableFrom(f.FieldType))
                .Select(f => f.Name)
                .ToArray();
        }

        static (Component comp, MethodInfo method) FindMethod(GameObject go, string methodName, bool hasArgument)
        {
            int expectedParams = hasArgument ? 1 : 0;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var method = comp.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == expectedParams);
                if (method != null)
                    return (comp, method);
            }
            return (null, null);
        }

        static string[] ListAvailableMethods(GameObject go)
        {
            var methods = new List<string>();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                foreach (var m in compType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (m.DeclaringType == typeof(object) || m.DeclaringType == typeof(Component)
                        || m.DeclaringType == typeof(MonoBehaviour) || m.DeclaringType == typeof(Behaviour))
                        continue;
                    var parms = m.GetParameters();
                    if (parms.Length <= 1)
                        methods.Add($"{compType.Name}.{m.Name}({string.Join(", ", parms.Select(p => p.ParameterType.Name))})");
                }
            }
            return methods.Distinct().ToArray();
        }

        static void AddTypedListener(UnityEventBase unityEvent, Component target, string methodName, string argument, string argumentType)
        {
            var resolvedType = argumentType?.ToLowerInvariant() ?? DetectArgumentType(argument);
            var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == methodName);

            switch (resolvedType)
            {
                case "int":
                    var intAction = (UnityAction<int>)Delegate.CreateDelegate(typeof(UnityAction<int>), target, method);
                    UnityEventTools.AddIntPersistentListener(unityEvent, intAction, int.Parse(argument));
                    break;
                case "float":
                    var floatAction = (UnityAction<float>)Delegate.CreateDelegate(typeof(UnityAction<float>), target, method);
                    UnityEventTools.AddFloatPersistentListener(unityEvent, floatAction, float.Parse(argument));
                    break;
                case "string":
                    var stringAction = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), target, method);
                    UnityEventTools.AddStringPersistentListener(unityEvent, stringAction, argument);
                    break;
                case "bool":
                    var boolAction = (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), target, method);
                    UnityEventTools.AddBoolPersistentListener(unityEvent, boolAction, bool.Parse(argument));
                    break;
                default:
                    var voidAction = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, method);
                    UnityEventTools.AddVoidPersistentListener(unityEvent, voidAction);
                    break;
            }
        }

        static string DetectArgumentType(string value)
        {
            if (int.TryParse(value, out _)) return "int";
            if (float.TryParse(value, out _)) return "float";
            if (bool.TryParse(value, out _)) return "bool";
            return "string";
        }
    }
}
