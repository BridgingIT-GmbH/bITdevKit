// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Microsoft.Extensions.Hosting;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Executes the with outbox operation.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static MessagingBuilderContext WithOutbox<TContext>(
        this MessagingBuilderContext context,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
    {
        context.WithBehavior<OutboxMessagePublisherBehavior<TContext>>();
        context.Services.AddOutboxMessageService<TContext>(optionsBuilder);

        return context;
    }

    /// <summary>
    /// Executes the with outbox operation.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static MessagingBuilderContext WithOutbox<TContext>(
        this MessagingBuilderContext context,
        OutboxMessageOptions options = null)
        where TContext : DbContext, IOutboxMessageContext
    {
        context.WithBehavior<OutboxMessagePublisherBehavior<TContext>>();
        context.Services.AddOutboxMessageService<TContext>(options);

        return context;
    }

    /// <summary>
    /// Adds outbox message service.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddOutboxMessageService<TContext>(
        this IServiceCollection services,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
    {
        return services.AddOutboxMessageService<TContext>(optionsBuilder(new OutboxMessageOptionsBuilder()).Build());
    }

    /// <summary>
    /// Adds outbox message service.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddOutboxMessageService<TContext>(
        this IServiceCollection services,
        OutboxMessageOptions options = null)
        where TContext : DbContext, IOutboxMessageContext
    {
        services.AddSingleton(options ?? new OutboxMessageOptions());
        services.AddSingleton<IOutboxMessageWorker, OutboxMessageWorker<TContext>>();
        services.AddSingleton<IOutboxMessageQueue>(sp => // needed by RepositoryOutboxDomainEventBehavior (optional)
            new OutboxMessageQueue(sp.GetRequiredService<ILoggerFactory>(),
                id => sp.GetRequiredService<IOutboxMessageWorker>().ProcessAsync(id)));

        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService<OutboxMessageService>();
            services.TryAddBackgroundServiceHealthCheck<OutboxMessageService>(
                $"{nameof(OutboxMessageService)}-{typeof(TContext).Name}",
                tags: ["background", "messaging", "outbox"]);
        }

        return services;
    }

    /// <summary>
    /// Adds outbox message service.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <typeparam name="TWorker">The worker type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddOutboxMessageService<TContext, TWorker>(
        this IServiceCollection services,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
        where TWorker : IOutboxMessageWorker
    {
        return services.AddOutboxMessageService<TContext, TWorker>(optionsBuilder(new OutboxMessageOptionsBuilder()).Build());
    }

    /// <summary>
    /// Adds outbox message service.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <typeparam name="TWorker">The worker type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection AddOutboxMessageService<TContext, TWorker>(
        this IServiceCollection services,
        OutboxMessageOptions options)
        where TContext : DbContext, IOutboxMessageContext
        where TWorker : IOutboxMessageWorker
    {
        services.AddSingleton(options ?? new OutboxMessageOptions());

        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService(sp =>
            new OutboxMessageService(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<TWorker>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetService<OutboxMessageOptions>()));
            services.TryAddBackgroundServiceHealthCheck<OutboxMessageService>(
                $"{nameof(OutboxMessageService)}-{typeof(TContext).Name}",
                tags: ["background", "messaging", "outbox"]);
        }

        return services;
    }
}
