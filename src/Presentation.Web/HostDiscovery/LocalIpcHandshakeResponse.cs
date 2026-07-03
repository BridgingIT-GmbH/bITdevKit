namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents a local IPC nonce handshake response.
/// </summary>
/// <param name="Ok">A value indicating whether the handshake succeeded.</param>
/// <param name="Feature">The feature name.</param>
/// <param name="ProtocolVersion">The protocol version.</param>
/// <param name="Error">The optional error message.</param>
public sealed record LocalIpcHandshakeResponse(bool Ok, string Feature, int ProtocolVersion, string Error);