namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents a local IPC nonce handshake request.
/// </summary>
/// <param name="Nonce">The endpoint nonce.</param>
/// <param name="Operation">The requested operation.</param>
public sealed record LocalIpcHandshakeRequest(string Nonce, string Operation);