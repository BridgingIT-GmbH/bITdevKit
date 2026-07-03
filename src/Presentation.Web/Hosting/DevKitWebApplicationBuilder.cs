namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps an ASP.NET Core <see cref="WebApplicationBuilder"/> with DevKit builder abstractions.
/// </summary>
/// <example>
/// <code>
/// var builder = DevKitWebApplication.CreateBuilder(args);
/// builder.Services.AddHealthChecks();
/// var app = builder.Build();
/// </code>
/// </example>
public sealed class DevKitWebApplicationBuilder : IDevKitApplicationBuilder
{
    private readonly WebApplicationBuilder inner;
    private readonly DevKitWebHostEnvironment devKitEnvironment;
    private readonly Dictionary<string, object> properties = new(StringComparer.OrdinalIgnoreCase);

    internal DevKitWebApplicationBuilder(WebApplicationBuilder inner, DevKitWebApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);

        this.inner = inner;
        this.devKitEnvironment = new DevKitWebHostEnvironment(inner.Environment);
        this.Options = options;
        this.LocalToolingDecision = DevKitLocalToolingPolicy.Evaluate(
            inner.Environment,
            inner.Configuration,
            options.Cli);

        this.properties[DevKitWebApplicationBuilderProperties.LocalToolingDecision] = this.LocalToolingDecision;
        this.properties[DevKitBuilderProperties.ApplicationName] = inner.Environment.ApplicationName;
        this.properties[DevKitBuilderProperties.ContentRootPath] = inner.Environment.ContentRootPath;
        this.properties[DevKitBuilderProperties.HostBuilder] = inner.Host;
        this.properties[DevKitBuilderProperties.LoggingBuilder] = inner.Logging;

        this.Services.AddSingleton(CreateStartupDiagnostics(inner, this.LocalToolingDecision));
        this.Services.AddHostedService<DevKitHostStartupDiagnosticsService>();

        if (this.LocalToolingDecision.Enabled)
        {
            this.RegisterLocalToolingServices();
        }
    }

    /// <summary>
    /// Gets the wrapped ASP.NET Core builder.
    /// </summary>
    public WebApplicationBuilder WebApplicationBuilder => this.inner;

    /// <summary>
    /// Gets the service collection used to register application services.
    /// </summary>
    public IServiceCollection Services => this.inner.Services;

    /// <summary>
    /// Gets the application configuration manager.
    /// </summary>
    public ConfigurationManager Configuration => this.inner.Configuration;

    /// <summary>
    /// Gets the web host environment.
    /// </summary>
    public IWebHostEnvironment Environment => this.inner.Environment;

    /// <summary>
    /// Gets the host builder.
    /// </summary>
    public ConfigureHostBuilder Host => this.inner.Host;

    /// <summary>
    /// Gets the web host builder.
    /// </summary>
    public ConfigureWebHostBuilder WebHost => this.inner.WebHost;

    /// <summary>
    /// Gets the logging builder.
    /// </summary>
    public ILoggingBuilder Logging => this.inner.Logging;

    /// <summary>
    /// Gets shared builder properties for feature-owned extensions.
    /// </summary>
    public IDictionary<string, object> Properties => this.properties;

    /// <summary>
    /// Gets the DevKit web application options.
    /// </summary>
    public DevKitWebApplicationOptions Options { get; }

    /// <summary>
    /// Gets the local tooling decision evaluated during builder creation.
    /// </summary>
    public DevKitLocalToolingDecision LocalToolingDecision { get; }

    IConfiguration IDevKitApplicationBuilder.Configuration => this.Configuration;

    IDevKitHostEnvironment IDevKitApplicationBuilder.Environment => this.devKitEnvironment;

    /// <summary>
    /// Applies an arbitrary builder configuration callback.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public DevKitWebApplicationBuilder Configure(Action<IDevKitApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(this);

        return this;
    }

    IDevKitApplicationBuilder IDevKitApplicationBuilder.Configure(Action<IDevKitApplicationBuilder> configure)
        => this.Configure(configure);

    /// <summary>
    /// Builds the ASP.NET Core web application.
    /// </summary>
    /// <returns>The built web application.</returns>
    public WebApplication Build()
        => this.inner.Build();

    private void RegisterLocalToolingServices()
    {
        this.Services.AddSingleton(CreateDescriptorOptions(this.inner));
        this.Services.AddSingleton<LocalIpcEndpointState>();
        this.Services.AddSingleton<HostRuntimeDescriptorWriter>();
        this.Services.AddSingleton<HostDescriptorCleanupService>();

        if (this.LocalToolingDecision.ConsoleCommandsEnabled)
        {
            this.Services.AddSingleton<IHostFeatureEndpointContributor, HostConsoleCommandEndpointContributor>();
            this.Services.AddHostedService<HostConsoleCommandIpcServer>();
        }

        if (this.LocalToolingDecision.McpEnabled)
        {
            this.Services.AddSingleton<McpDispatcher>();
            this.Services.AddSingleton<McpServerSessionReader>();
            this.Services.AddMcpHandler<RuntimeDiagnosticsMcpHandler>();
            this.Services.AddMcpHandler<LogEntryMcpHandler>();
            this.Services.AddSingleton<IHostFeatureEndpointContributor, McpHostFeatureEndpointContributor>();
            this.Services.AddHostedService<McpIpcServer>();
        }

        this.Services.AddHostedService<HostDescriptorLifecycleService>();
    }

    private static DevKitHostStartupDiagnostics CreateStartupDiagnostics(
        WebApplicationBuilder builder,
        DevKitLocalToolingDecision decision)
    {
        var features = GetEnabledFeatures(decision).ToArray();

        return new DevKitHostStartupDiagnostics(
            "web",
            builder.Environment.ApplicationName,
            builder.Environment.EnvironmentName,
            builder.Environment.ContentRootPath,
            decision.Enabled,
            decision.Enabled,
            null,
            decision.Enabled,
            decision.ConsoleCommandsEnabled,
            decision.McpEnabled,
            features,
            decision.Reason);
    }

    private static IEnumerable<string> GetEnabledFeatures(DevKitLocalToolingDecision decision)
    {
        if (!decision.Enabled)
        {
            yield break;
        }

        if (decision.ConsoleCommandsEnabled)
        {
            yield return "consoleCommands";
        }

        if (decision.McpEnabled)
        {
            yield return "mcp";
        }
    }

    private static HostDescriptorOptions CreateDescriptorOptions(WebApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath;
        var workspacePath = WorkspacePathUtilities.ResolveWorkspaceRoot(contentRoot);

        return new HostDescriptorOptions
        {
            RegistryPath = HostDescriptorPath.GetDefaultRegistryPath(),
            RuntimeId = CreateRuntimeId(builder.Environment.ApplicationName),
            WorkspacePath = workspacePath,
            ProjectPath = ResolveProjectPath(contentRoot),
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    private static string CreateRuntimeId(string applicationName)
        => HostRuntimeNaming.CreateRuntimeId(applicationName, System.Environment.ProcessId);

    private static string ResolveProjectPath(string contentRoot)
        => Directory.EnumerateFiles(contentRoot, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();

}
