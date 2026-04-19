export interface PluginEntry {
    type: "local";
    path: string;
}
export declare function discoverPlugins(projectDir?: string, homeDir?: string): PluginEntry[];
