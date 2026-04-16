using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UniClaude.Editor
{
    /// <summary>
    /// Shared YAML parsing utilities for Unity scene and prefab files.
    /// Extracts GameObject names and script GUIDs from Unity YAML content.
    /// </summary>
    public static class UnityYamlParser
    {
        // Matches m_Name only within a GameObject block (after "--- !u!1" header).
        // Unity YAML uses "--- !u!<classID>" document separators; classID 1 = GameObject.
        static readonly Regex GameObjectBlockRegex = new(
            @"--- !u!1 &\d+.*?(?=--- !u!|\z)",
            RegexOptions.Singleline);

        static readonly Regex NameInBlockRegex = new(@"m_Name:\s*(.+)$", RegexOptions.Multiline);

        static readonly Regex ScriptGuidRegex = new(@"m_Script:\s*\{.*guid:\s*(\w+)", RegexOptions.Multiline);

        /// <summary>
        /// Extracts GameObject names from Unity YAML content.
        /// Only captures m_Name from GameObject blocks (classID 1), avoiding
        /// names from Transform, MonoBehaviour, and other component types.
        /// </summary>
        /// <param name="content">Raw Unity YAML file content.</param>
        /// <returns>List of unique non-empty GameObject names.</returns>
        public static List<string> ExtractGameObjectNames(string content)
        {
            var names = new List<string>();
            foreach (Match block in GameObjectBlockRegex.Matches(content))
            {
                var nameMatch = NameInBlockRegex.Match(block.Value);
                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                        names.Add(name);
                }
            }
            return names;
        }

        /// <summary>
        /// Extracts unique script GUIDs referenced via m_Script fields.
        /// </summary>
        /// <param name="content">Raw Unity YAML file content.</param>
        /// <returns>List of unique script GUIDs.</returns>
        public static List<string> ExtractScriptGuids(string content)
        {
            var guids = new List<string>();
            foreach (Match m in ScriptGuidRegex.Matches(content))
            {
                var guid = m.Groups[1].Value;
                if (!guids.Contains(guid))
                    guids.Add(guid);
            }
            return guids;
        }
    }
}
