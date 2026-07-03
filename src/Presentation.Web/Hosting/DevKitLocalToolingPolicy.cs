namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Evaluates whether a DevKit web host should register local CLI integration.
/// </summary>
/// <example>
/// <code>
/// var decision = DevKitLocalToolingPolicy.Evaluate(environment, configuration, options);
/// </code>
/// </example>
public static class DevKitLocalToolingPolicy
{
    /// <summary>
    /// Evaluates local CLI integration eligibility.
    /// </summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="options">The local CLI integration options.</param>
    /// <returns>The local tooling decision.</returns>
    public static DevKitLocalToolingDecision Evaluate(
        IHostEnvironment environment,
        IConfiguration configuration,
        DevKitCliHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return DevKitLocalToolingDecision.Disabled("Disabled by options.");
        }

        if (!environment.IsDevelopment() && !options.AllowOutsideDevelopmentForTests)
        {
            return DevKitLocalToolingDecision.Disabled("Host environment is not Development.");
        }

        if (configuration.GetValue<bool?>("DevKit:Cli:Enabled") == false)
        {
            return DevKitLocalToolingDecision.Disabled("Disabled by configuration.");
        }

        return DevKitLocalToolingDecision.EnabledFor(
            options.ConsoleCommandsEnabled && configuration.GetValue<bool?>("DevKit:Cli:ConsoleCommands") != false,
            options.McpEnabled && configuration.GetValue<bool?>("DevKit:Cli:Mcp") != false);
    }
}