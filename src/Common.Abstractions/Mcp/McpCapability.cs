namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes one app-side MCP operation.
/// </summary>
/// <example>
/// <code>
/// var capability = new McpCapability("logs.query", "diagnostics", "logs", "Queries bounded log entries.");
/// </code>
/// </example>
/// <param name="Name">The operation name.</param>
/// <param name="Toolset">The required toolset.</param>
/// <param name="Feature">The owning feature name.</param>
/// <param name="Description">The agent-facing description.</param>
public sealed record McpCapability(string Name, string Toolset, string Feature, string Description)
{
    /// <summary>
    /// Gets or initializes the capability category.
    /// </summary>
    public string Category { get; init; } = "inspect";

    /// <summary>
    /// Gets or initializes the capability owner.
    /// </summary>
    public string Owner { get; init; } = "bdk";

    /// <summary>
    /// Gets or initializes bounded argument schema metadata.
    /// </summary>
    public object ArgumentSchema { get; init; } = new { type = "object", additionalProperties = true };
}
