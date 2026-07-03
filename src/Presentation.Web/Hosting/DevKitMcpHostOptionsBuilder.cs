namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Builds MCP host capability options for local CLI integration.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args, options => options
///     .Cli(cli => cli.Mcp(mcp => mcp.Enabled(false))));
/// </code>
/// </example>
public sealed class DevKitMcpHostOptionsBuilder(DevKitCliHostOptions options)
{
    /// <summary>
    /// Enables or disables MCP hosting.
    /// </summary>
    /// <param name="enabled">A value indicating whether MCP hosting is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitMcpHostOptionsBuilder Enabled(bool enabled = true)
    {
        options.McpEnabled = enabled;

        return this;
    }
}