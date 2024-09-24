// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using EntityFrameworkCore;
using Logging;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxDomainEventService<TContext>(
        this IServiceCollection services,
        Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
        where TContext : DbContext, IOutboxDomainEventContext
    {
        return services.AddOutboxDomainEventService<TContext>(optionsBuilder(new OutboxDomainEventOptionsBuilder())
            .Build());
    }

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
        services.AddHostedService<OutboxDomainEventService>();

        return services;
    }

    public static IServiceCollection AddOutboxDomainEventService<TContext, TWorker>(
        this IServiceCollection services,
        Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
        where TContext : DbContext, IOutboxDomainEventContext
        where TWorker : IOutboxDomainEventWorker
    {
        return services.AddOutboxDomainEventService<TContext, TWorker>(
            optionsBuilder(new OutboxDomainEventOptionsBuilder()).Build());
    }

    public static IServiceCollection AddOutboxDomainEventService<TContext, TWorker>(
        this IServiceCollection services,
        OutboxDomainEventOptions options)
        where TContext : DbContext, IOutboxDomainEventContext
        where TWorker : IOutboxDomainEventWorker
    {
        services.AddSingleton(options ?? new OutboxDomainEventOptions());
        services.AddHostedService(sp => new OutboxDomainEventService(sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<TWorker>(),
            sp.GetService<OutboxDomainEventOptions>()));

        return services;
    }
}