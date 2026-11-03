namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents a discovered host descriptor with validation status.
/// </summary>
public sealed class HostRuntimeInfo
{
    /// <summary>
    /// Gets the descriptor file path.
    /// </summary>
    public string DescriptorPath { get; init; }

    /// <summary>
    /// Gets the parsed host runtime descriptor.
    /// </summary>
    public HostRuntimeDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets the descriptor validation status.
    /// </summary>
    public HostRuntimeStatus Status { get; init; }

    /// <summary>
    /// Gets the validation reason when the host is not ready.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Determines whether this host belongs to the supplied workspace. A host started as a development app instance
    /// also matches when the workspace is its worktree root, so a host whose project sits below a nested solution
    /// is still found from the worktree root.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    /// <returns><see langword="true" /> when the descriptor workspace or worktree root matches.</returns>
    public bool MatchesWorkspace(CliWorkspaceContext workspace)
        => PathEquals(this.Descriptor?.WorkspacePath, workspace.Path) ||
            PathEquals(this.Descriptor?.DevAppInstance?.WorktreeRoot, workspace.Path);

    /// <summary>
    /// Gets the primary URL of the host, preferring HTTPS.
    /// </summary>
    /// <returns>The primary URL, or <see langword="null" /> when the descriptor carries none.</returns>
    public string GetPrimaryUrl()
        => this.Descriptor?.Urls?.FirstOrDefault(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ??
            this.Descriptor?.Urls?.FirstOrDefault() ??
            this.Descriptor?.DevAppInstance?.HttpsUrl ??
            this.Descriptor?.DevAppInstance?.HttpUrl;

    private static bool PathEquals(string path, string other)
        => !string.IsNullOrWhiteSpace(path) &&
            string.Equals(Normalize(path), Normalize(other), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string Normalize(string path)
        => string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
