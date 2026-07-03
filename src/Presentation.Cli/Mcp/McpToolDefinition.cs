namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Describes a stable CLI MCP tool.
/// </summary>
/// <example>
/// <code>
/// var tool = new McpToolDefinition("bdk_mcp_status", "Returns MCP status.", new { type = "object" });
/// </code>
/// </example>
/// <param name="Name">The tool name.</param>
/// <param name="Description">The tool description.</param>
/// <param name="InputSchema">The JSON schema for arguments.</param>
public sealed record McpToolDefinition(string Name, string Description, object InputSchema);
