namespace UniClaude.Editor.Installer
{
    /// <summary>Install state of the UniClaude package.</summary>
    public enum InstallMode
    {
        /// <summary>Installed via UPM (git URL). Lives in Library/PackageCache/.</summary>
        Standard,
        /// <summary>Embedded clone in Packages/, git-invisible via filter and exclude.</summary>
        Ninja,
        /// <summary>Other (local path, hand-rolled embed, or not installed).</summary>
        Other,
    }
}
