namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Builds MCP host options for generic hosts.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args, options => options
///     .Cli(cli => cli.Mcp(false)));
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

    /// <summary>
    /// Disables an MCP feature by name.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitMcpHostOptionsBuilder DisableFeature(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);

        options.DisabledMcpFeatures.Add(featureName);

        return this;
    }

    /// <summary>
    /// Uses the content root as the workspace path.
    /// </summary>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitMcpHostOptionsBuilder WorkspacePathFromContentRoot()
    {
        options.UseContentRootAsWorkspacePath = true;

        return this;
    }
}