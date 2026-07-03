namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
/// Represents the local IPC envelope for app-side MCP operations.
/// </summary>
/// <example>
/// <code>
/// var request = new McpIpcRequest("nonce", 1, "logs.query", "diagnostics", JsonDocument.Parse("{}").RootElement);
/// </code>
/// </example>
/// <param name="Nonce">The runtime endpoint nonce.</param>
/// <param name="ProtocolVersion">The MCP IPC protocol version.</param>
/// <param name="Operation">The MCP operation name.</param>
/// <param name="Toolset">The required toolset.</param>
/// <param name="Arguments">The operation arguments.</param>
public sealed record McpIpcRequest(string Nonce, int ProtocolVersion, string Operation, string Toolset, JsonElement Arguments);
