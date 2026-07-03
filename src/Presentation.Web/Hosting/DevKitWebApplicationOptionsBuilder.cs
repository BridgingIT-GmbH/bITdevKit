namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Builds <see cref="DevKitWebApplicationOptions"/> instances.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args, options => options
///     .Cli(cli => cli.Enabled(false)));
/// </code>
/// </example>
public sealed class DevKitWebApplicationOptionsBuilder(DevKitWebApplicationOptions options)
{
    /// <summary>
    /// Configures local CLI integration options.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitWebApplicationOptionsBuilder Cli(Action<DevKitCliHostOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(new DevKitCliHostOptionsBuilder(options.Cli));

        return this;
    }
}