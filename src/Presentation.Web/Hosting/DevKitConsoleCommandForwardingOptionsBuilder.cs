namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Builds Console Command forwarding options for local CLI integration.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args, options => options
///     .Cli(cli => cli.ConsoleCommands(commands => commands.Enabled(false))));
/// </code>
/// </example>
public sealed class DevKitConsoleCommandForwardingOptionsBuilder(DevKitCliHostOptions options)
{
    /// <summary>
    /// Enables or disables Console Command forwarding.
    /// </summary>
    /// <param name="enabled">A value indicating whether Console Command forwarding is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitConsoleCommandForwardingOptionsBuilder Enabled(bool enabled = true)
    {
        options.ConsoleCommandsEnabled = enabled;

        return this;
    }
}