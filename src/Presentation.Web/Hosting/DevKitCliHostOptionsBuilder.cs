namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Builds <see cref="DevKitCliHostOptions"/> instances.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args, options => options
///     .Cli(cli => cli.ConsoleCommands(false)));
/// </code>
/// </example>
public sealed class DevKitCliHostOptionsBuilder(DevKitCliHostOptions options)
{
    /// <summary>
    /// Enables or disables all local CLI integration.
    /// </summary>
    /// <param name="enabled">A value indicating whether local CLI integration is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitCliHostOptionsBuilder Enabled(bool enabled = true)
    {
        options.Enabled = enabled;

        return this;
    }

    /// <summary>
    /// Enables or disables Console Command forwarding.
    /// </summary>
    /// <param name="enabled">A value indicating whether Console Command forwarding is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitCliHostOptionsBuilder ConsoleCommands(bool enabled = true)
    {
        options.ConsoleCommandsEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Configures Console Command forwarding options.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitCliHostOptionsBuilder ConsoleCommands(Action<DevKitConsoleCommandForwardingOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DevKitConsoleCommandForwardingOptionsBuilder(options);
        configure(builder);

        return this;
    }

    /// <summary>
    /// Enables or disables MCP hosting.
    /// </summary>
    /// <param name="enabled">A value indicating whether MCP hosting is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitCliHostOptionsBuilder Mcp(bool enabled = true)
    {
        options.McpEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Configures MCP hosting options.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitCliHostOptionsBuilder Mcp(Action<DevKitMcpHostOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DevKitMcpHostOptionsBuilder(options);
        configure(builder);

        return this;
    }

    /// <summary>
    /// Allows local CLI integration outside Development for tests.
    /// </summary>
    /// <param name="enabled">A value indicating whether the test override is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitCliHostOptionsBuilder AllowOutsideDevelopmentForTests(bool enabled = true)
    {
        options.AllowOutsideDevelopmentForTests = enabled;

        return this;
    }
}