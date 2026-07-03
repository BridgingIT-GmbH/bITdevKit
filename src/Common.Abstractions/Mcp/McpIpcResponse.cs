namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the local IPC response envelope for app-side MCP operations.
/// </summary>
/// <example>
/// <code>
/// var response = McpIpcResponse.Success(McpResponse.Success("OK"));
/// </code>
/// </example>
/// <param name="Ok">A value indicating whether the IPC request was accepted.</param>
/// <param name="ProtocolVersion">The MCP IPC protocol version.</param>
/// <param name="Response">The MCP operation response.</param>
/// <param name="Error">The IPC-level error message.</param>
public sealed record McpIpcResponse(bool Ok, int ProtocolVersion, McpResponse Response, string Error)
{
    /// <summary>
    /// Creates a successful IPC response.
    /// </summary>
    /// <param name="response">The MCP response.</param>
    /// <returns>The IPC response.</returns>
    public static McpIpcResponse Success(McpResponse response)
        => new(true, 1, response, null);

    /// <summary>
    /// Creates a failed IPC response.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="error">The IPC-level error.</param>
    /// <returns>The IPC response.</returns>
    public static McpIpcResponse Failure(string code, string summary, string error)
        => new(false, 1, McpResponse.Unavailable(code, summary, error), error);
}
