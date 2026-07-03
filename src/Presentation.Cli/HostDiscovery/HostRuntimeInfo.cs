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
    /// Determines whether this host belongs to the supplied workspace.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    /// <returns><see langword="true" /> when the descriptor workspace matches.</returns>
    public bool MatchesWorkspace(CliWorkspaceContext workspace)
        => string.Equals(Normalize(this.Descriptor?.WorkspacePath), Normalize(workspace.Path), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string Normalize(string path)
        => string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
