namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
/// Represents an app-side MCP operation request sent over local IPC.
/// </summary>
/// <example>
/// <code>
/// var request = new McpRequest("logs.query", "diagnostics", JsonDocument.Parse("{}").RootElement);
/// </code>
/// </example>
/// <param name="Operation">The operation name, such as <c>logs.query</c>.</param>
/// <param name="Toolset">The required toolset for the operation.</param>
/// <param name="Arguments">The operation arguments.</param>
public sealed record McpRequest(string Operation, string Toolset, JsonElement Arguments);
