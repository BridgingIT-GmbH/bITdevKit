namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Represents command-line arguments after global CLI option parsing.
/// </summary>
public sealed class ParsedCommandLine
{
    /// <summary>
    /// Gets command arguments that remain after global options are removed.
    /// </summary>
    public string[] CommandArguments { get; init; } = [];

    /// <summary>
    /// Gets the explicit workspace path supplied by the user.
    /// </summary>
    public string WorkspacePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether verbose output is enabled.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Gets a value indicating whether quiet output is enabled.
    /// </summary>
    public bool Quiet { get; init; }

    /// <summary>
    /// Gets a value indicating whether color output is disabled.
    /// </summary>
    public bool NoColor { get; init; }

    /// <summary>
    /// Gets a value indicating whether the startup banner is disabled.
    /// </summary>
    public bool NoLogo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the startup banner is explicitly requested.
    /// </summary>
    public bool Banner { get; init; }

    /// <summary>
    /// Gets a value indicating whether interactive behavior is disabled.
    /// </summary>
    public bool NonInteractive { get; init; }

    /// <summary>
    /// Gets a value indicating whether help output was requested.
    /// </summary>
    public bool ShowHelp { get; init; }

    /// <summary>
    /// Gets a value indicating whether version output was requested.
    /// </summary>
    public bool ShowVersion { get; init; }

    /// <summary>
    /// Gets the requested output format.
    /// </summary>
    public CliOutputFormat OutputFormat { get; init; } = CliOutputFormat.Text;

    /// <summary>
    /// Gets an argument parsing error when parsing failed.
    /// </summary>
    public string Error { get; init; }
}
