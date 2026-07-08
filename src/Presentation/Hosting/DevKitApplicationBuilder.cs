namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps a <see cref="HostApplicationBuilder"/> with DevKit builder abstractions.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitApplication.CreateBuilder(args);
/// builder.Services.AddHostedService&lt;Worker&gt;();
/// using var host = builder.Build();
/// </code>
/// </example>
public sealed class DevKitApplicationBuilder : IDevKitHostApplicationBuilder
{
    private readonly HostApplicationBuilder inner;
    private readonly DevKitHostEnvironment devKitEnvironment;
    private readonly Dictionary<string, object> properties = new(StringComparer.OrdinalIgnoreCase);

    internal DevKitApplicationBuilder(HostApplicationBuilder inner, DevKitApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);

        this.inner = inner;
        this.devKitEnvironment = new DevKitHostEnvironment(inner.Environment);
        this.Options = options;

        this.properties[DevKitBuilderProperties.ApplicationName] = inner.Environment.ApplicationName;
        this.properties[DevKitBuilderProperties.ContentRootPath] = inner.Environment.ContentRootPath;
        this.properties[DevKitBuilderProperties.HostEnvironment] = inner.Environment;
        this.properties[DevKitBuilderProperties.HostApplicationBuilder] = inner;
        this.properties[DevKitBuilderProperties.LoggingBuilder] = inner.Logging;

        this.Services.AddSingleton(CreateStartupDiagnostics(inner, options));
        this.Services.AddHostedService<DevKitHostStartupDiagnosticsService>();
    }

    /// <summary>
    /// Gets the wrapped generic host application builder.
    /// </summary>
    public HostApplicationBuilder HostApplicationBuilder => this.inner;

    /// <summary>
    /// Gets the service collection used to register application services.
    /// </summary>
    public IServiceCollection Services => this.inner.Services;

    /// <summary>
    /// Gets the application configuration manager.
    /// </summary>
    public ConfigurationManager Configuration => this.inner.Configuration;

    /// <summary>
    /// Gets the host environment.
    /// </summary>
    public IHostEnvironment Environment => this.inner.Environment;

    /// <summary>
    /// Gets the logging builder.
    /// </summary>
    public ILoggingBuilder Logging => this.inner.Logging;

    /// <summary>
    /// Gets shared builder properties for feature-owned extensions.
    /// </summary>
    public IDictionary<string, object> Properties => this.properties;

    /// <summary>
    /// Gets the DevKit application options.
    /// </summary>
    public DevKitApplicationOptions Options { get; }

    IConfiguration IDevKitApplicationBuilder.Configuration => this.Configuration;

    IDevKitHostEnvironment IDevKitApplicationBuilder.Environment => this.devKitEnvironment;

    /// <summary>
    /// Applies an arbitrary builder configuration callback.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitApplicationBuilder Configure(Action<IDevKitApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(this);

        return this;
    }

    IDevKitApplicationBuilder IDevKitApplicationBuilder.Configure(Action<IDevKitApplicationBuilder> configure)
        => this.Configure(configure);

    /// <summary>
    /// Builds the generic host application.
    /// </summary>
    /// <returns>The built host.</returns>
    public IHost Build()
        => this.inner.Build();

    private static DevKitHostStartupDiagnostics CreateStartupDiagnostics(
        HostApplicationBuilder builder,
        DevKitApplicationOptions options)
    {
        var features = GetEnabledFeatures(options.Cli).ToArray();

        return new DevKitHostStartupDiagnostics(
            "generic",
            builder.Environment.ApplicationName,
            builder.Environment.EnvironmentName,
            builder.Environment.ContentRootPath,
            options.Cli.Enabled,
            false,
            null,
            options.Cli.Enabled,
            options.Cli.Enabled && options.Cli.ConsoleCommandsEnabled,
            options.Cli.Enabled && options.Cli.McpEnabled,
            features,
            options.Cli.Enabled ? "Enabled by options." : "Disabled by default.");
    }

    private static IEnumerable<string> GetEnabledFeatures(DevKitCliHostOptions options)
    {
        if (!options.Enabled)
        {
            yield break;
        }

        if (options.ConsoleCommandsEnabled)
        {
            yield return "consoleCommands";
        }

        if (options.McpEnabled)
        {
            yield return "mcp";
        }
    }
}
