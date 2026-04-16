using System.Collections.Generic;

namespace UniClaude.Editor
{
    /// <summary>
    /// Static definitions of health check steps for each tier.
    /// </summary>
    public static class HealthCheckSteps
    {
        /// <summary>
        /// Light tier: quick smoke test covering sidecar connection, file I/O, and scene manipulation.
        /// </summary>
        public static List<HealthCheckStep> Light => new()
        {
            new HealthCheckStep
            {
                Name = "file_write",
                Category = "File operations",
                Prompt = "Write a file at Assets/UniClaudeHealthCheck/write-test.txt with the content 'healthcheck'",
                ExpectedTool = "file_write",
            },
            new HealthCheckStep
            {
                Name = "file_read",
                Category = "File operations",
                Prompt = "Call the file_read tool with path 'Assets/UniClaudeHealthCheck/ping.txt'",
                ExpectedTool = "file_read",
            },
            new HealthCheckStep
            {
                Name = "scene_create_gameobject",
                Category = "Scene tools",
                Prompt = "Create an empty GameObject named 'HC_TestObject' in the current scene",
                ExpectedTool = "scene_create_gameobject",
            },
            new HealthCheckStep
            {
                Name = "component_add",
                Category = "Component tools",
                Prompt = "Add a BoxCollider component to HC_TestObject",
                ExpectedTool = "component_add",
            },
            new HealthCheckStep
            {
                Name = "scene_delete_gameobject",
                Category = "Scene tools",
                Prompt = "Delete the GameObject named 'HC_TestObject'",
                ExpectedTool = "scene_delete_gameobject",
            },
        };

        /// <summary>
        /// Complete tier: exercises all MCP tool categories.
        /// Steps are ordered so they build on each other — earlier failures cascade.
        /// </summary>
        public static List<HealthCheckStep> Complete => new()
        {
            // File operations (steps 1-2)
            new HealthCheckStep
            {
                Name = "file_write",
                Category = "File operations",
                Prompt = "Write a file at Assets/UniClaudeHealthCheck/write-test.txt with the content 'healthcheck'",
                ExpectedTool = "file_write",
            },
            new HealthCheckStep
            {
                Name = "file_read",
                Category = "File operations",
                Prompt = "Call the file_read tool with path 'Assets/UniClaudeHealthCheck/ping.txt'",
                ExpectedTool = "file_read",
            },

            // Scene + component creation (steps 3-4, kept alive for later steps)
            new HealthCheckStep
            {
                Name = "scene_create_gameobject",
                Category = "Scene tools",
                Prompt = "Create an empty GameObject named 'HC_TestObject' in the current scene",
                ExpectedTool = "scene_create_gameobject",
            },
            new HealthCheckStep
            {
                Name = "component_add",
                Category = "Component tools",
                Prompt = "Add a BoxCollider component to HC_TestObject",
                ExpectedTool = "component_add",
            },

            // More file operations (steps 5-8)
            new HealthCheckStep
            {
                Name = "file_find",
                Category = "File operations",
                Prompt = "Find all .txt files in Assets/UniClaudeHealthCheck/",
                ExpectedTool = "file_find",
            },
            new HealthCheckStep
            {
                Name = "file_create_script",
                Category = "File operations",
                Prompt = "Create a MonoBehaviour script at Assets/UniClaudeHealthCheck/HCTest.cs",
                ExpectedTool = "file_create_script",
            },
            new HealthCheckStep
            {
                Name = "file_modify_script",
                Category = "File operations",
                Prompt = "In Assets/UniClaudeHealthCheck/HCTest.cs, replace 'void Start' with 'void Awake'",
                ExpectedTool = "file_modify_script",
            },
            new HealthCheckStep
            {
                Name = "file_delete",
                Category = "File operations",
                Prompt = "Delete the file Assets/UniClaudeHealthCheck/HCTest.cs",
                ExpectedTool = "file_delete",
            },

            // Scene operations (steps 9-10)
            new HealthCheckStep
            {
                Name = "scene_get_hierarchy",
                Category = "Scene tools",
                Prompt = "List all objects in the current scene",
                ExpectedTool = "scene_get_hierarchy",
            },
            new HealthCheckStep
            {
                Name = "scene_rename_gameobject",
                Category = "Scene tools",
                Prompt = "Rename HC_TestObject to HC_Renamed",
                ExpectedTool = "scene_rename_gameobject",
            },

            // Component operations (steps 11-13)
            new HealthCheckStep
            {
                Name = "component_get_all",
                Category = "Component tools",
                Prompt = "List all components on HC_Renamed",
                ExpectedTool = "component_get_all",
            },
            new HealthCheckStep
            {
                Name = "component_set_property",
                Category = "Component tools",
                Prompt = "Set the BoxCollider size to (2,2,2) on HC_Renamed",
                ExpectedTool = "component_set_property",
            },
            new HealthCheckStep
            {
                Name = "inspector_inspect",
                Category = "Inspector tools",
                Prompt = "Inspect the BoxCollider on HC_Renamed",
                ExpectedTool = "inspector_inspect",
            },

            // Prefab operations (steps 14-15)
            new HealthCheckStep
            {
                Name = "prefab_create",
                Category = "Prefab tools",
                Prompt = "Create a prefab from HC_Renamed at Assets/UniClaudeHealthCheck/HCPrefab.prefab",
                ExpectedTool = "prefab_create",
            },
            new HealthCheckStep
            {
                Name = "prefab_get_contents",
                Category = "Prefab tools",
                Prompt = "Show the contents of Assets/UniClaudeHealthCheck/HCPrefab.prefab",
                ExpectedTool = "prefab_get_contents",
            },

            // Material operations (steps 16-17)
            new HealthCheckStep
            {
                Name = "material_create",
                Category = "Material tools",
                Prompt = "Create a material at Assets/UniClaudeHealthCheck/HCMat.mat using Standard shader",
                ExpectedTool = "material_create",
            },
            new HealthCheckStep
            {
                Name = "material_assign",
                Category = "Material tools",
                Prompt = "Assign Assets/UniClaudeHealthCheck/HCMat.mat to HC_Renamed",
                ExpectedTool = "material_assign",
            },

            // Tag operations (steps 18-19)
            new HealthCheckStep
            {
                Name = "tag_create",
                Category = "Tag/Layer tools",
                Prompt = "Create a tag named 'HCTestTag'",
                ExpectedTool = "tag_create",
            },
            new HealthCheckStep
            {
                Name = "tag_delete",
                Category = "Tag/Layer tools",
                Prompt = "Delete the tag 'HCTestTag'",
                ExpectedTool = "tag_delete",
            },

            // Scene object cleanup (step 20)
            new HealthCheckStep
            {
                Name = "scene_delete_gameobject",
                Category = "Scene tools",
                Prompt = "Delete the GameObject named 'HC_Renamed'",
                ExpectedTool = "scene_delete_gameobject",
            },

            // Layer operations (step 21)
            new HealthCheckStep
            {
                Name = "layer_list",
                Category = "Tag/Layer tools",
                Prompt = "List all layers in the project",
                ExpectedTool = "layer_list",
            },

            // Asset operations (steps 22-23)
            new HealthCheckStep
            {
                Name = "asset_find",
                Category = "Asset tools",
                Prompt = "Find all .prefab assets in Assets/UniClaudeHealthCheck/",
                ExpectedTool = "asset_find",
            },
            new HealthCheckStep
            {
                Name = "asset_get_info",
                Category = "Asset tools",
                Prompt = "Get info about Assets/UniClaudeHealthCheck/HCPrefab.prefab",
                ExpectedTool = "asset_get_info",
            },

            // Project operations (step 24)
            new HealthCheckStep
            {
                Name = "project_get_console_log",
                Category = "Project tools",
                Prompt = "Show recent console log entries",
                ExpectedTool = "project_get_console_log",
            },

            // Scene management (step 25)
            new HealthCheckStep
            {
                Name = "scene_save",
                Category = "Scene management",
                Prompt = "Save the current scene to Assets/UniClaudeHealthCheck/HCScene.unity",
                ExpectedTool = "scene_save",
            },
        };
    }
}
