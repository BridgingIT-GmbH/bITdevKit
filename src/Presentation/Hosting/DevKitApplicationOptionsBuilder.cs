namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Builds <see cref="DevKitApplicationOptions"/> instances.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args, options => options
///     .Cli(cli => cli.Enabled()));
/// </code>
/// </example>
public sealed class DevKitApplicationOptionsBuilder(DevKitApplicationOptions options)
{
    /// <summary>
    /// Configures local CLI integration options.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitApplicationOptionsBuilder Cli(Action<DevKitCliHostOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(new DevKitCliHostOptionsBuilder(options.Cli));

        return this;
    }

    /// <summary>
    /// Enables or disables local tooling integration.
    /// </summary>
    /// <param name="enabled">A value indicating whether local tooling integration is enabled.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitApplicationOptionsBuilder LocalTooling(bool enabled = true)
    {
        options.Cli.Enabled = enabled;

        return this;
    }
}