namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Orchestrations;

/// <summary>
/// Adds operational orchestration endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the orchestration endpoints from the fluent orchestration builder with a fluent options builder.
    /// </summary>
    public static OrchestrationBuilderContext AddEndpoints(
        this OrchestrationBuilderContext context,
        Builder<OrchestrationEndpointsOptionsBuilder, OrchestrationEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        var options = optionsBuilder?.Invoke(new OrchestrationEndpointsOptionsBuilder()).Build();
        return context.AddEndpoints(options, enabled);
    }

    /// <summary>
    /// Registers the orchestration endpoints from the fluent orchestration builder with explicit options.
    /// </summary>
    public static OrchestrationBuilderContext AddEndpoints(
        this OrchestrationBuilderContext context,
        OrchestrationEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            context.AddMcpHandlers(enabled);

            if (options is not null)
            {
                context.Services.AddSingleton(options);
            }

            context.Services.AddEndpoints<OrchestrationEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Registers the orchestration endpoints from the fluent orchestration builder with default options.
    /// </summary>
    public static OrchestrationBuilderContext AddEndpoints(this OrchestrationBuilderContext context, bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            context.AddMcpHandlers(enabled);
            context.Services.AddEndpoints<OrchestrationEndpoints>(enabled);
        }

        return context;
    }

    /// <summary>
    /// Registers orchestration console commands from the fluent orchestration builder.
    /// </summary>
    public static OrchestrationBuilderContext AddConsoleCommands(this OrchestrationBuilderContext context, bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            context.AddMcpHandlers(enabled);
            context.Services.AddTransient<IConsoleCommand, OrchestrationDefinitionsConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationListConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationHistoryConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationMetricsConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationSignalConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationPauseConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationResumeConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationCancelConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationTerminateConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationArchiveConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationReleaseLeaseConsoleCommand>();
            context.Services.AddTransient<IConsoleCommand, OrchestrationRequeueTimersConsoleCommand>();
        }

        return context;
    }

    /// <summary>
    /// Registers the orchestration endpoints with a fluent options builder.
    /// </summary>
    public static IServiceCollection AddOrchestrationEndpoints(
        this IServiceCollection services,
        Builder<OrchestrationEndpointsOptionsBuilder, OrchestrationEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = optionsBuilder?.Invoke(new OrchestrationEndpointsOptionsBuilder()).Build();
        return services.AddOrchestrationEndpoints(options, enabled);
    }

    /// <summary>
    /// Registers the orchestration endpoints with explicit options.
    /// </summary>
    public static IServiceCollection AddOrchestrationEndpoints(
        this IServiceCollection services,
        OrchestrationEndpointsOptions options,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            services.AddOrchestrationMcpHandlers(enabled);

            if (options is not null)
            {
                services.AddSingleton(options);
            }

            services.AddEndpoints<OrchestrationEndpoints>(enabled);
        }

        return services;
    }

    /// <summary>
    /// Registers the orchestration endpoints with default options.
    /// </summary>
    public static IServiceCollection AddOrchestrationEndpoints(this IServiceCollection services, bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            services.AddOrchestrationMcpHandlers(enabled);
            services.AddEndpoints<OrchestrationEndpoints>(enabled);
        }

        return services;
    }

    public static OrchestrationBuilderContext AddMcpHandlers(
        this OrchestrationBuilderContext context,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (enabled)
        {
            context.Services.AddMcpHandler<OrchestrationMcpHandler>();
        }

        return context;
    }

    public static IServiceCollection AddOrchestrationMcpHandlers(this IServiceCollection services, bool enabled = true)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        if (enabled)
        {
            services.AddMcpHandler<OrchestrationMcpHandler>();
        }

        return services;
    }
}
