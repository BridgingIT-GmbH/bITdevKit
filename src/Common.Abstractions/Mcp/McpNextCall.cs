namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes an advisory next MCP tool call for an agent.
/// </summary>
/// <example>
/// <code>
/// var next = new McpNextCall("bdk_capabilities_get", new { });
/// </code>
/// </example>
/// <param name="Tool">The MCP tool name.</param>
/// <param name="Arguments">The tool arguments.</param>
public sealed record McpNextCall(string Tool, object Arguments);
