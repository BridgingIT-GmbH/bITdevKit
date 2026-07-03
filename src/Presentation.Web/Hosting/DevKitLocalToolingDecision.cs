namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents the evaluated host-side local tooling decision.
/// </summary>
/// <param name="Enabled">A value indicating whether local tooling is enabled.</param>
/// <param name="ConsoleCommandsEnabled">A value indicating whether Console Command forwarding is enabled.</param>
/// <param name="McpEnabled">A value indicating whether MCP hosting is enabled.</param>
/// <param name="Reason">The decision reason.</param>
/// <example>
/// <code>
/// var decision = DevKitLocalToolingDecision.Disabled("Disabled by options.");
/// </code>
/// </example>
public sealed record DevKitLocalToolingDecision(
    bool Enabled,
    bool ConsoleCommandsEnabled,
    bool McpEnabled,
    string Reason)
{
    /// <summary>
    /// Creates an enabled local tooling decision.
    /// </summary>
    /// <param name="consoleCommands">A value indicating whether Console Command forwarding is enabled.</param>
    /// <param name="mcp">A value indicating whether MCP hosting is enabled.</param>
    /// <returns>The enabled decision.</returns>
    public static DevKitLocalToolingDecision EnabledFor(bool consoleCommands, bool mcp)
        => new(true, consoleCommands, mcp, "Enabled.");

    /// <summary>
    /// Creates a disabled local tooling decision.
    /// </summary>
    /// <param name="reason">The disabled reason.</param>
    /// <returns>The disabled decision.</returns>
    public static DevKitLocalToolingDecision Disabled(string reason)
        => new(false, false, false, reason);
}