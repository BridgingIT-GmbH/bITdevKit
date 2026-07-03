namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Describes the workspace used for CLI host discovery and selections.
/// </summary>
public sealed class CliWorkspaceContext
{
    /// <summary>
    /// Gets the normalized workspace path.
    /// </summary>
    public string Path { get; init; }

    /// <summary>
    /// Gets the stable workspace hash used for selection files.
    /// </summary>
    public string Hash { get; init; }
}
