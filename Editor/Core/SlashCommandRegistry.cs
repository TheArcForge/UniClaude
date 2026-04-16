using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UniClaude.Editor
{
    /// <summary>
    /// A selectable argument choice for a slash command.
    /// </summary>
    public class ArgChoice
    {
        /// <summary>The value to insert (e.g. "sonnet").</summary>
        public string Value;

        /// <summary>Display label shown in autocomplete (e.g. "Sonnet 4.6").</summary>
        public string Label;

        /// <summary>Optional description (e.g. "Best for everyday tasks").</summary>
        public string Description;
    }
}

namespace UniClaude.Editor
{
    /// <summary>
    /// Source of a slash command — local commands are handled in-editor,
    /// CLI commands are forwarded to the Claude Code process.
    /// </summary>
    public enum CommandSource
    {
        /// <summary>Handled locally in the editor (e.g. /clear, /new).</summary>
        Local,

        /// <summary>Forwarded to the Claude Code CLI as a prompt.</summary>
        Cli
    }

    /// <summary>
    /// Defines a slash command that can be executed in the chat window.
    /// </summary>
    public class SlashCommand
    {
        /// <summary>The command name without the leading slash (e.g. "clear").</summary>
        public string Name;

        /// <summary>Short description shown in autocomplete and help.</summary>
        public string Description;

        /// <summary>Whether this command accepts/requires arguments.</summary>
        public bool AcceptsArgs;

        /// <summary>Predefined argument choices shown in autocomplete (may be null).</summary>
        public List<ArgChoice> ArgChoices;

        /// <summary>Where this command is handled.</summary>
        public CommandSource Source;

        /// <summary>
        /// The action to execute for local commands. Receives the argument string (may be null).
        /// Null for CLI commands — those are forwarded to the bridge.
        /// </summary>
        public Action<string> Execute;

        /// <summary>
        /// Absolute path to the source <c>.md</c> file for CLI commands.
        /// Used to read the prompt content at dispatch time.
        /// Null for local commands.
        /// </summary>
        public string FilePath;
    }

    /// <summary>
    /// Registry of all available slash commands. Discovers commands from:
    /// <list type="bullet">
    ///   <item>Local commands registered by the chat window</item>
    ///   <item>User-level commands from <c>~/.claude/commands/</c></item>
    ///   <item>Project-level commands from <c>.claude/commands/</c></item>
    ///   <item>Plugin commands from <c>~/.claude/plugins/</c></item>
    ///   <item>UPM package commands from <c>Packages/*/.claude/commands/</c></item>
    /// </list>
    /// </summary>
    public class SlashCommandRegistry
    {
        readonly List<SlashCommand> _commands = new();

        /// <summary>
        /// All registered commands, sorted alphabetically.
        /// </summary>
        public IReadOnlyList<SlashCommand> Commands => _commands;

        /// <summary>
        /// Registers a local command handled in-editor.
        /// </summary>
        /// <summary>
        /// Registers a fully constructed local command (for commands with arg choices).
        /// </summary>
        /// <param name="command">The command to register.</param>
        public void RegisterLocal(SlashCommand command)
        {
            _commands.RemoveAll(c => string.Equals(c.Name, command.Name, StringComparison.OrdinalIgnoreCase));
            command.Source = CommandSource.Local;
            _commands.Add(command);
            Sort();
        }

        /// <param name="name">Command name without the leading slash.</param>
        /// <param name="description">Short description for help/autocomplete.</param>
        /// <param name="execute">Action to run. Receives the argument string after the command name.</param>
        /// <param name="acceptsArgs">Whether the command accepts arguments (autocomplete won't auto-execute).</param>
        public void RegisterLocal(string name, string description, Action<string> execute, bool acceptsArgs = false)
        {
            // Replace if exists
            _commands.RemoveAll(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            _commands.Add(new SlashCommand
            {
                Name = name,
                Description = description,
                AcceptsArgs = acceptsArgs,
                Source = CommandSource.Local,
                Execute = execute
            });
            Sort();
        }

        /// <summary>
        /// Discovers CLI commands from the file system — user commands, project commands,
        /// installed plugin commands, and UPM package commands. Each <c>.md</c> file in a
        /// <c>commands/</c> directory becomes a command, with its description parsed from
        /// YAML frontmatter.
        /// </summary>
        public void DiscoverCliCommands()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect existing local command names so we don't overwrite them
            foreach (var cmd in _commands)
                if (cmd.Source == CommandSource.Local)
                    seen.Add(cmd.Name);

            // Remove old CLI commands before re-scan
            _commands.RemoveAll(c => c.Source == CommandSource.Cli);

            // 1. User-level commands: ~/.claude/commands/
            ScanDirectory(Path.Combine(home, ".claude", "commands"), seen);

            // 2. Project-level commands: .claude/commands/
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            ScanDirectory(Path.Combine(projectRoot, ".claude", "commands"), seen);

            // 3. Plugin commands: ~/.claude/plugins/marketplaces/*/plugins/*/commands/
            var pluginsRoot = Path.Combine(home, ".claude", "plugins", "marketplaces");
            if (Directory.Exists(pluginsRoot))
            {
                foreach (var marketplace in Directory.GetDirectories(pluginsRoot))
                {
                    var pluginsDir = Path.Combine(marketplace, "plugins");
                    if (!Directory.Exists(pluginsDir)) continue;

                    foreach (var plugin in Directory.GetDirectories(pluginsDir))
                    {
                        var commandsDir = Path.Combine(plugin, "commands");
                        ScanDirectory(commandsDir, seen);
                    }
                }
            }

            // 4. UPM package commands
            var packagesDir = Path.Combine(projectRoot, "Packages");

            // 4a. Embedded packages (directories directly in Packages/)
            ScanPackageCommands(packagesDir, seen);

            // 4b. Local packages (file: references in manifest.json)
            var manifestPath = Path.Combine(packagesDir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var content = File.ReadAllText(manifestPath);
                    foreach (Match match in Regex.Matches(content, @"""file:([^""]+)"""))
                    {
                        var resolved = Path.GetFullPath(Path.Combine(packagesDir, match.Groups[1].Value));
                        ScanDirectory(Path.Combine(resolved, ".claude", "commands"), seen);
                    }
                }
                catch { /* ignore manifest parse errors */ }
            }

            Sort();
            Debug.Log($"[UniClaude] Discovered {_commands.Count(c => c.Source == CommandSource.Cli)} CLI commands " +
                       $"(user + project + plugins + packages), " +
                       $"{_commands.Count(c => c.Source == CommandSource.Local)} local commands");
        }

        /// <summary>
        /// Scans a directory for <c>.md</c> command files and registers them.
        /// </summary>
        void ScanDirectory(string dir, HashSet<string> seen)
        {
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (seen.Contains(name)) continue;
                seen.Add(name);

                var description = ParseFrontmatterDescription(file);
                if (description != null && description.StartsWith("Deprecated", StringComparison.OrdinalIgnoreCase))
                    continue;

                _commands.Add(new SlashCommand
                {
                    Name = name,
                    Description = description ?? $"Run /{name}",
                    AcceptsArgs = true,
                    Source = CommandSource.Cli,
                    FilePath = Path.GetFullPath(file)
                });
            }
        }

        /// <summary>
        /// Scans UPM packages under the given directory for <c>.claude/commands/</c> and
        /// registers any <c>.md</c> command files found. Only local/embedded packages are
        /// scanned — packages in the global cache are excluded.
        /// </summary>
        /// <param name="packagesDir">Path to the project's <c>Packages/</c> directory.</param>
        /// <param name="seen">Set of command names already registered (prevents overwrites).</param>
        internal void ScanPackageCommands(string packagesDir, HashSet<string> seen)
        {
            if (!Directory.Exists(packagesDir)) return;

            foreach (var packageDir in Directory.GetDirectories(packagesDir))
            {
                var commandsDir = Path.Combine(packageDir, ".claude", "commands");
                ScanDirectory(commandsDir, seen);
            }
        }

        /// <summary>
        /// Parses the YAML frontmatter <c>description</c> field from a command <c>.md</c> file.
        /// </summary>
        static string ParseFrontmatterDescription(string path)
        {
            try
            {
                using var reader = new StreamReader(path);
                var firstLine = reader.ReadLine();
                if (firstLine == null || firstLine.Trim() != "---")
                    return null;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line == null || line.Trim() == "---")
                        break;

                    if (line.TrimStart().StartsWith("description:"))
                    {
                        var value = line.Substring(line.IndexOf(':') + 1).Trim();
                        // Strip surrounding quotes
                        if (value.Length >= 2 &&
                            ((value[0] == '"' && value[^1] == '"') ||
                             (value[0] == '\'' && value[^1] == '\'')))
                        {
                            value = value[1..^1];
                        }
                        return value;
                    }
                }
            }
            catch
            {
                // Ignore malformed files
            }

            return null;
        }

        /// <summary>
        /// Reads a CLI command <c>.md</c> file and returns the body after the YAML frontmatter.
        /// Returns null if the file cannot be read.
        /// </summary>
        /// <param name="path">Absolute path to the <c>.md</c> command file.</param>
        /// <returns>The prompt body (everything after the closing <c>---</c>), trimmed.</returns>
        internal static string ReadCommandBody(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path);
                var i = 0;

                // Skip opening ---
                if (i < lines.Length && lines[i].Trim() == "---")
                {
                    i++;
                    // Skip until closing ---
                    while (i < lines.Length && lines[i].Trim() != "---")
                        i++;
                    if (i < lines.Length) i++; // skip the closing ---
                }

                // Join remaining lines
                var body = string.Join("\n", lines, i, lines.Length - i).Trim();
                return body.Length > 0 ? body : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds an exact command match by name.
        /// </summary>
        /// <param name="name">Command name without the leading slash.</param>
        /// <returns>The command, or null if not found.</returns>
        public SlashCommand Find(string name)
        {
            return _commands.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns commands whose name starts with the given prefix (for autocomplete).
        /// </summary>
        /// <param name="prefix">The prefix to match (without the leading slash).</param>
        /// <returns>Matching commands ordered alphabetically.</returns>
        public List<SlashCommand> Match(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return new List<SlashCommand>(_commands);

            return _commands
                .Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Tries to parse and execute a local slash command from user input.
        /// Returns the command if found (caller handles CLI commands separately).
        /// </summary>
        /// <param name="input">The full user input starting with '/'.</param>
        /// <returns>The matched command, or null if not found.</returns>
        public SlashCommand Parse(string input)
        {
            if (string.IsNullOrEmpty(input) || input[0] != '/')
                return null;

            var trimmed = input.Substring(1).Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            var name = spaceIndex >= 0 ? trimmed.Substring(0, spaceIndex) : trimmed;

            return Find(name);
        }

        /// <summary>
        /// Extracts the argument portion from a slash command input.
        /// </summary>
        /// <param name="input">The full user input starting with '/'.</param>
        /// <returns>The argument string after the command name, or null.</returns>
        public static string ParseArgs(string input)
        {
            if (string.IsNullOrEmpty(input) || input[0] != '/')
                return null;

            var trimmed = input.Substring(1).Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            return spaceIndex >= 0 ? trimmed.Substring(spaceIndex + 1).Trim() : null;
        }

        void Sort()
        {
            _commands.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }
    }
}
