namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Contains output settings resolved from global CLI options.
/// </summary>
public sealed class CliOutputSettings
{
    /// <summary>
    /// Gets the requested output format.
    /// </summary>
    public CliOutputFormat Format { get; init; } = CliOutputFormat.Text;

    /// <summary>
    /// Gets a value indicating whether non-essential output is suppressed.
    /// </summary>
    public bool Quiet { get; init; }

    /// <summary>
    /// Gets a value indicating whether verbose diagnostic output is enabled.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Gets a value indicating whether ANSI color output is disabled.
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
    /// Gets a value indicating whether the CLI is running in a CI environment.
    /// </summary>
    public bool IsCi { get; init; }

    /// <summary>
    /// Gets a value indicating whether JSON output is requested.
    /// </summary>
    public bool IsJson => this.Format == CliOutputFormat.Json;
}
