namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents a local Console Command IPC request.
/// </summary>
/// <param name="Nonce">The endpoint nonce.</param>
/// <param name="ProtocolVersion">The protocol version.</param>
/// <param name="Operation">The operation name.</param>
/// <param name="CommandLine">The command line to execute.</param>
/// <param name="RawTokens">The original command tokens to execute.</param>
public sealed record LocalConsoleCommandIpcRequest(
    string Nonce,
    int ProtocolVersion,
    string Operation,
    string CommandLine,
    string[] RawTokens);
