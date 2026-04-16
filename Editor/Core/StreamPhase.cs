namespace UniClaude.Editor
{
    /// <summary>Streaming phase of a Claude response.</summary>
    public enum StreamPhase
    {
        None,
        Thinking,
        Writing,
        ToolUse,
    }
}
