namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Contains resolved invocation state for command execution.
/// </summary>
public sealed class CliInvocationContext
{
    /// <summary>
    /// Gets the command arguments after global option parsing.
    /// </summary>
    public string[] Arguments { get; init; } = [];

    /// <summary>
    /// Gets the resolved workspace context.
    /// </summary>
    public CliWorkspaceContext Workspace { get; init; }

    /// <summary>
    /// Gets the output settings.
    /// </summary>
    public CliOutputSettings Output { get; init; }

    /// <summary>
    /// Gets the invocation cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
