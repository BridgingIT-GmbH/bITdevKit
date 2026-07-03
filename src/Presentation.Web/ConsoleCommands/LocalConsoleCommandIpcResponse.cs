namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents a local Console Command IPC response.
/// </summary>
/// <param name="Ok">A value indicating whether the request was accepted.</param>
/// <param name="ProtocolVersion">The protocol version.</param>
/// <param name="ExitCode">The command exit code.</param>
/// <param name="Output">The captured command output.</param>
/// <param name="Error">The optional error message.</param>
public sealed record LocalConsoleCommandIpcResponse(bool Ok, int ProtocolVersion, int ExitCode, string Output, string Error);
