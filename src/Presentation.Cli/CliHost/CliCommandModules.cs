namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers root CLI commands.
/// </summary>
public sealed class CoreCliCommandModule : ICliCommandModule
{
    /// <inheritdoc />
    public string Name => "core";

    /// <inheritdoc />
    public string Description => "Core CLI help and version commands";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, CliCommandModuleContext context)
    {
    }

    /// <inheritdoc />
    public void RegisterCommands(ICliCommandRegistry registry)
    {
        registry.WithCommand<VersionCliCommand>();
    }
}

/// <summary>
/// Registers documentation commands.
/// </summary>
public sealed class DocsCliCommandModule : ICliCommandModule
{
    /// <inheritdoc />
    public string Name => "docs";

    /// <inheritdoc />
    public string Description => "Official bITdevKit documentation";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, CliCommandModuleContext context)
    {
        services.AddSingleton<IDocsConsoleCommandRuntime, CliDocsConsoleCommandRuntime>();
    }

    /// <inheritdoc />
    public void RegisterCommands(ICliCommandRegistry registry)
    {
        registry.WithCommand<DocsConsoleCommand>();
    }
}

/// <summary>
/// Registers shared host registry commands.
/// </summary>
public sealed class HostsCliCommandModule : ICliCommandModule
{
    /// <inheritdoc />
    public string Name => "hosts";

    /// <inheritdoc />
    public string Description => "Shared host discovery and selection commands";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, CliCommandModuleContext context)
    {
    }

    /// <inheritdoc />
    public void RegisterCommands(ICliCommandRegistry registry)
        => registry.AddGroup("hosts", this.Description, group =>
        {
            group.WithCommand<HostsListCliCommand>();
            group.WithCommand<HostsCurrentCliCommand>();
            group.WithCommand<HostsSelectCliCommand>();
            group.WithCommand<HostsRefreshCliCommand>();
            group.WithCommand<HostsVersionsCliCommand>();
            group.WithCommand<HostsCleanCliCommand>();
            group.WithCommand<HostsKillCliCommand>();
        });
}

/// <summary>
/// Registers host Console Command forwarding commands.
/// </summary>
public sealed class HostForwardingCliCommandModule : ICliCommandModule
{
    /// <inheritdoc />
    public string Name => "host";

    /// <inheritdoc />
    public string Description => "Host Console Command forwarding";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, CliCommandModuleContext context)
    {
    }

    /// <inheritdoc />
    public void RegisterCommands(ICliCommandRegistry registry)
        => registry.AddGroup("host", this.Description, group => group.WithCommand<HostRunCliCommand>());
}

/// <summary>
/// Registers the STDIO MCP command module.
/// </summary>
public sealed class McpCliCommandModule : ICliCommandModule
{
    /// <inheritdoc />
    public string Name => "mcp";

    /// <inheritdoc />
    public string Description => "STDIO MCP server and runtime diagnostics tools";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, CliCommandModuleContext context)
    {
        services.AddSingleton<McpToolCatalog>();
        services.AddSingleton<McpIpcClient>();
        services.AddSingleton<McpRuntimeTools>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMcpDocumentationSource, GitHubMcpDocumentationSource>();
        services.AddSingleton<IMcpApiReferenceSource, GitHubPagesMcpApiReferenceSource>();
        services.AddSingleton<McpDocumentationTools>();
        services.AddSingleton<McpApiReferenceTools>();
        services.AddSingleton<McpGuidanceTools>();
        services.AddSingleton<McpToolExecutor>();
        services.AddSingleton<McpServerSessionPublisher>();
        services.AddSingleton<StdioMcpServer>();
    }

    /// <inheritdoc />
    public void RegisterCommands(ICliCommandRegistry registry)
    {
    }
}