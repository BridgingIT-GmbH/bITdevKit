namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Defines stable CLI process exit codes.
/// </summary>
public enum CliExitCode
{
    /// <summary>
    /// The command completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The command executed but failed.
    /// </summary>
    CommandFailed = 1,

    /// <summary>
    /// Command-line arguments were invalid.
    /// </summary>
    InvalidArguments = 2,

    /// <summary>
    /// No compatible host was found.
    /// </summary>
    HostNotFound = 3,

    /// <summary>
    /// Multiple compatible hosts require an explicit selection.
    /// </summary>
    HostSelectionRequired = 4,

    /// <summary>
    /// The selected host is stale, unreachable, or incompatible.
    /// </summary>
    SelectedHostUnavailable = 5,

    /// <summary>
    /// The CLI and host protocol versions are incompatible.
    /// </summary>
    ProtocolVersionMismatch = 6,

    /// <summary>
    /// An unexpected CLI error occurred.
    /// </summary>
    InternalError = 7
}
