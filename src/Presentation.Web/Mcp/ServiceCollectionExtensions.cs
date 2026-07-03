namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers app-side MCP handlers.
/// </summary>
/// <example>
/// <code>
/// services.AddMcpHandler&lt;CommerceMcpHandler&gt;();
/// </code>
/// </example>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Registers a single MCP handler.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMcpHandler<THandler>(this IServiceCollection services)
        where THandler : class, IMcpHandler
    {
        services.AddMcpStartupDiagnostics();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IMcpHandler, THandler>());

        return services;
    }

    /// <summary>
    /// Registers MCP handlers from the assembly containing <typeparamref name="TMarker"/>.
    /// </summary>
    /// <typeparam name="TMarker">The marker type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMcpHandlersFromAssembly<TMarker>(this IServiceCollection services)
        => services.AddMcpHandlersFromAssembly(typeof(TMarker).Assembly);

    /// <summary>
    /// Registers MCP handlers from an assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMcpHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        services.AddMcpStartupDiagnostics();

        foreach (var handlerType in assembly.SafeGetTypes<IMcpHandler>())
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IMcpHandler), handlerType));
        }

        return services;
    }

    private static IServiceCollection AddMcpStartupDiagnostics(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, McpStartupDiagnosticsService>());

        return services;
    }
}
