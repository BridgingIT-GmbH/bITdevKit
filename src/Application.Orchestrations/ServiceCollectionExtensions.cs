// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

/// <summary>
/// Provides dependency injection registration helpers for orchestration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the orchestration foundation services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">An optional registration callback.</param>
    /// <returns>The orchestration builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddOrchestrations()
    ///     .WithOrchestration&lt;SampleOrchestration&gt;();
    /// </code>
    /// </example>
    public static OrchestrationBuilderContext AddOrchestrations(
        this IServiceCollection services,
        Action<OrchestrationBuilderContext> optionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureRegistrationStore(services);
        services.TryAddSingleton(new OrchestrationExecutionSettings());
        services.TryAddSingleton<IOrchestrationClock, SystemOrchestrationClock>();
        services.TryAddSingleton<InMemoryOrchestrationExecutor>();
        services.TryAddSingleton<IOrchestrationExecutor>(serviceProvider => serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>());
        services.TryAddSingleton<IOrchestrationService>(serviceProvider => serviceProvider.GetRequiredService<InMemoryOrchestrationExecutor>());
        services.TryAddSingleton<OrchestrationRecoveryService>();
        if (!IsBuildTimeOpenApiGeneration())
        {
            services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OrchestrationRecoveryService>());
            services.TryAddBackgroundServiceHealthCheck<OrchestrationRecoveryService>(
                nameof(OrchestrationRecoveryService),
                tags: ["background", "orchestrations"]);
        }
        services.TryAddSingleton<IOrchestrationQueryService>(serviceProvider =>
            new OrchestrationQueryService(serviceProvider.GetRequiredService<IOrchestrationQueryStore>()));
        services.TryAddSingleton<IOrchestrationAdministrationService>(serviceProvider =>
            new OrchestrationAdministrationService(
                serviceProvider.GetRequiredService<IOrchestrationQueryStore>(),
                serviceProvider.GetRequiredService<IOrchestrationAdministrationStore>()));
        services.AddDiagramRendering();
        services.TryAddSingleton<OrchestrationDefinitionDiagramProjector>();
        services.TryAddSingleton<OrchestrationInstanceDiagramProjector>();
        services.TryAddSingleton<IOrchestrationDiagramService, OrchestrationDiagramService>();
        services.AddInMemoryOrchestrationPersistence();

        var context = new OrchestrationBuilderContext(services);
        optionsAction?.Invoke(context);

        return context;
    }

    /// <summary>
    /// Registers a code-first orchestration definition.
    /// </summary>
    /// <typeparam name="TOrchestration">The orchestration type.</typeparam>
    /// <param name="context">The builder context.</param>
    /// <returns>The current builder context.</returns>
    public static OrchestrationBuilderContext WithOrchestration<TOrchestration>(this OrchestrationBuilderContext context)
        where TOrchestration : class
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.AddTransient<TOrchestration>();
        EnsureRegistrationStore(context.Services).Add(typeof(TOrchestration));

        return context;
    }

    /// <summary>
    /// Adds a behavior that wraps orchestration activity execution by registering a behavior type.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type implementing <see cref="IOrchestrationBehavior"/>.</typeparam>
    /// <param name="context">The orchestration builder context.</param>
    /// <param name="behavior">An optional pre-instantiated behavior.</param>
    /// <returns>The current builder context.</returns>
    public static OrchestrationBuilderContext WithBehavior<TBehavior>(
        this OrchestrationBuilderContext context,
        IOrchestrationBehavior behavior = null)
        where TBehavior : class, IOrchestrationBehavior
    {
        ArgumentNullException.ThrowIfNull(context);

        if (behavior is null)
        {
            context.Services.AddScoped<IOrchestrationBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IOrchestrationBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Adds a behavior that wraps orchestration activity execution using a factory method.
    /// </summary>
    /// <param name="context">The orchestration builder context.</param>
    /// <param name="implementationFactory">The behavior factory.</param>
    /// <returns>The current builder context.</returns>
    public static OrchestrationBuilderContext WithBehavior(
        this OrchestrationBuilderContext context,
        Func<IServiceProvider, IOrchestrationBehavior> implementationFactory)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (implementationFactory is not null)
        {
            context.Services.AddScoped(typeof(IOrchestrationBehavior), implementationFactory);
        }

        return context;
    }

    /// <summary>
    /// Adds a pre-instantiated behavior that wraps orchestration activity execution.
    /// </summary>
    /// <param name="context">The orchestration builder context.</param>
    /// <param name="behavior">The behavior instance.</param>
    /// <returns>The current builder context.</returns>
    public static OrchestrationBuilderContext WithBehavior(
        this OrchestrationBuilderContext context,
        IOrchestrationBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IOrchestrationBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Adds the in-memory orchestration persistence provider and exposes its store interfaces.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInMemoryOrchestrationPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IOrchestrationStorageProvider>(serviceProvider =>
            new InMemoryOrchestrationStorageProvider(
                serviceProvider.GetService<ISerializer>() ?? new SystemTextJsonSerializer(),
                serviceProvider.GetService<ICurrentUserAccessor>(),
                serviceProvider.GetService<IOrchestrationClock>()));

        services.TryAddSingleton(serviceProvider =>
            (InMemoryOrchestrationStorageProvider)serviceProvider.GetRequiredService<IOrchestrationStorageProvider>());
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().Instances);
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().Leases);
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().History);
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().Signals);
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().Timers);
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().Queries);
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOrchestrationStorageProvider>().Administration);

        return services;
    }

    private static OrchestrationRegistrationStore EnsureRegistrationStore(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(OrchestrationRegistrationStore));
        if (descriptor?.ImplementationInstance is OrchestrationRegistrationStore registrations)
        {
            return registrations;
        }

        registrations = new OrchestrationRegistrationStore();
        services.AddSingleton(registrations);

        return registrations;
    }

    private static bool IsBuildTimeOpenApiGeneration()
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }
}
