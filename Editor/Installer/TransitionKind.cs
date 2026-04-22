namespace UniClaude.Editor.Installer
{
    /// <summary>
    /// Identifies which transition the progress window / helper is finalizing.
    /// The string forms ("to-standard", "delete-from-ninja") match the Node
    /// command names and are stored in pending-transition.json as the `kind` field.
    /// </summary>
    public enum TransitionKind
    {
        /// <summary>Convert Ninja → Standard (restore manifest entry, delete embedded folder).</summary>
        ToStandard,
        /// <summary>Delete UniClaude entirely from a Ninja-mode install.</summary>
        DeleteFromNinja,
    }

    /// <summary>Conversions between <see cref="TransitionKind"/> and the Node-side string identifier.</summary>
    public static class TransitionKindExtensions
    {
        /// <summary>Returns the Node-side string ("to-standard" or "delete-from-ninja").</summary>
        public static string ToWireString(this TransitionKind kind) => kind switch
        {
            TransitionKind.ToStandard => "to-standard",
            TransitionKind.DeleteFromNinja => "delete-from-ninja",
            _ => throw new System.ArgumentException($"unknown kind: {kind}"),
        };

        /// <summary>Parses a Node-side string into a <see cref="TransitionKind"/>.</summary>
        public static TransitionKind FromWireString(string s) => s switch
        {
            "to-standard" => TransitionKind.ToStandard,
            "delete-from-ninja" => TransitionKind.DeleteFromNinja,
            _ => throw new System.ArgumentException($"unknown wire string: {s}"),
        };
    }
}
