// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides registration helpers for the Entity Framework backed domain event outbox service.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the domain event outbox service using a fluent options builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The fluent options builder used to customize outbox processing.</param>
    /// <returns>The current <see cref="IServiceCollection" /> for further composition.</returns>
    /// <example>
    /// <code>
    /// services.AddOutboxDomainEventService&lt;AppDbContext&gt;(options => options
    ///     .ProcessingInterval(TimeSpan.FromSeconds(10))
    ///     .LeaseDuration(TimeSpan.FromSeconds(30))
    ///     .LeaseRenewalInterval(TimeSpan.FromSeconds(10)));
    /// </code>
    /// </example>
    public static IServiceCollection AddOutboxDomainEventService<TContext>(
        this IServiceCollection services,
        Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        return services.AddOutboxDomainEventService<TContext>(
            optionsBuilder(new OutboxDomainEventOptionsBuilder()).Build());
    }

    /// <summary>
    /// Registers the default domain event outbox worker and hosted service.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The outbox processing options.</param>
    /// <returns>The current <see cref="IServiceCollection" /> for further composition.</returns>
    /// <example>
    /// <code>
    /// services.AddOutboxDomainEventService&lt;AppDbContext&gt;(new OutboxDomainEventOptions
    /// {
    ///     ProcessingInterval = TimeSpan.FromSeconds(10),
    ///     LeaseDuration = TimeSpan.FromSeconds(30)
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOutboxDomainEventService<TContext>(
        this IServiceCollection services,
        OutboxDomainEventOptions options)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        services.AddSingleton(options ?? new OutboxDomainEventOptions());
        services.AddSingleton<IOutboxDomainEventWorker, OutboxDomainEventWorker<TContext>>();
        services.AddSingleton<IOutboxDomainEventQueue>(sp => // needed by OutboxMessagePublisherBehavior (optional)
            new OutboxDomainEventQueue(sp.GetRequiredService<ILoggerFactory>(),
                id => sp.GetRequiredService<IOutboxDomainEventWorker>().ProcessAsync(id)));

        if (!IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService<OutboxDomainEventService>();
        }

        return services;
    }

    /// <summary>
    /// Registers a custom domain event outbox worker using a fluent options builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
    /// <typeparam name="TWorker">The custom worker type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The fluent options builder used to customize outbox processing.</param>
    /// <returns>The current <see cref="IServiceCollection" /> for further composition.</returns>
    /// <example>
    /// <code>
    /// services.AddOutboxDomainEventService&lt;AppDbContext, CustomOutboxWorker&gt;(options => options
    ///     .ProcessingInterval(TimeSpan.FromSeconds(5))
    ///     .LeaseDuration(TimeSpan.FromSeconds(20)));
    /// </code>
    /// </example>
    public static IServiceCollection AddOutboxDomainEventService<TContext, TWorker>(
        this IServiceCollection services,
        Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
        where TContext : DbContext, IOutboxDomainEventContext
        where TWorker : class, IOutboxDomainEventWorker
    {
        return services.AddOutboxDomainEventService<TContext, TWorker>(
            optionsBuilder(new OutboxDomainEventOptionsBuilder()).Build());
    }

    /// <summary>
    /// Registers a custom domain event outbox worker and hosted service.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IOutboxDomainEventContext" />.</typeparam>
    /// <typeparam name="TWorker">The custom worker type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The outbox processing options.</param>
    /// <returns>The current <see cref="IServiceCollection" /> for further composition.</returns>
    /// <example>
    /// <code>
    /// services.AddOutboxDomainEventService&lt;AppDbContext, CustomOutboxWorker&gt;(new OutboxDomainEventOptions
    /// {
    ///     ProcessingInterval = TimeSpan.FromSeconds(5),
    ///     LeaseDuration = TimeSpan.FromSeconds(20)
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOutboxDomainEventService<TContext, TWorker>(
        this IServiceCollection services,
        OutboxDomainEventOptions options)
        where TContext : DbContext, IOutboxDomainEventContext
        where TWorker : class, IOutboxDomainEventWorker
    {
        services.AddSingleton(options ?? new OutboxDomainEventOptions());
        services.TryAddSingleton<TWorker>();
        services.TryAddSingleton<IOutboxDomainEventWorker>(sp => sp.GetRequiredService<TWorker>());
        services.TryAddSingleton<IOutboxDomainEventQueue>(sp =>
            new OutboxDomainEventQueue(sp.GetRequiredService<ILoggerFactory>(),
                id => sp.GetRequiredService<IOutboxDomainEventWorker>().ProcessAsync(id)));

        if (!IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService(sp =>
            new OutboxDomainEventService(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<TWorker>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetService<IDatabaseReadyService>(),
                sp.GetService<OutboxDomainEventOptions>()));
        }

        return services;
    }
}
