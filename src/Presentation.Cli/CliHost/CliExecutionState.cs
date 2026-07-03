namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Stores the process exit code selected by a CLI console command.
/// </summary>
public sealed class CliExecutionState
{
    /// <summary>
    /// Gets or sets the process exit code.
    /// </summary>
    public CliExitCode ExitCode { get; set; } = CliExitCode.Success;
}