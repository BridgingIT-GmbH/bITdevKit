namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides the DevKit-aware entry point for generic host applications.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args);
/// using var host = builder.Build();
/// await host.RunAsync();
/// </code>
/// </example>
public static class DevKitApplication
{
    /// <summary>
    /// Creates a DevKit-aware generic host application builder.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The DevKit application builder.</returns>
    public static DevKitApplicationBuilder CreateBuilder(string[] args)
        => CreateBuilder(args, configureOptions: null);

    /// <summary>
    /// Creates a DevKit-aware generic host application builder with explicit options.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="configure">The options configuration callback.</param>
    /// <returns>The DevKit application builder.</returns>
    public static DevKitApplicationBuilder CreateBuilder(
        string[] args,
        Action<DevKitApplicationOptionsBuilder> configureOptions)
    {
        var hostBuilder = Host.CreateApplicationBuilder(args);
        var options = new DevKitApplicationOptions();
        configureOptions?.Invoke(new DevKitApplicationOptionsBuilder(options));

        return new DevKitApplicationBuilder(hostBuilder, options);
    }

    /// <summary>
    /// Creates and configures a DevKit-aware generic host application builder.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="configure">The builder configuration callback.</param>
    /// <returns>The DevKit application builder.</returns>
    public static DevKitApplicationBuilder CreateBuilder(
        string[] args,
        Action<DevKitApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = CreateBuilder(args);
        configure(builder);

        return builder;
    }
}