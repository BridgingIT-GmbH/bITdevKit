// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Microsoft.Extensions.Hosting;

public static partial class ServiceCollectionExtensions
{
    public static MessagingBuilderContext WithOutbox<TContext>(
        this MessagingBuilderContext context,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
    {
        context.WithBehavior<OutboxMessagePublisherBehavior<TContext>>();
        context.Services.AddOutboxMessageService<TContext>(optionsBuilder);

        return context;
    }

    public static MessagingBuilderContext WithOutbox<TContext>(
        this MessagingBuilderContext context,
        OutboxMessageOptions options = null)
        where TContext : DbContext, IOutboxMessageContext
    {
        context.WithBehavior<OutboxMessagePublisherBehavior<TContext>>();
        context.Services.AddOutboxMessageService<TContext>(options);

        return context;
    }

    public static IServiceCollection AddOutboxMessageService<TContext>(
        this IServiceCollection services,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
    {
        return services.AddOutboxMessageService<TContext>(optionsBuilder(new OutboxMessageOptionsBuilder()).Build());
    }

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

        if (!IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService<OutboxMessageService>();
        }

        return services;
    }

    public static IServiceCollection AddOutboxMessageService<TContext, TWorker>(
        this IServiceCollection services,
        Builder<OutboxMessageOptionsBuilder, OutboxMessageOptions> optionsBuilder)
        where TContext : DbContext, IOutboxMessageContext
        where TWorker : IOutboxMessageWorker
    {
        return services.AddOutboxMessageService<TContext, TWorker>(optionsBuilder(new OutboxMessageOptionsBuilder()).Build());
    }

    public static IServiceCollection AddOutboxMessageService<TContext, TWorker>(
        this IServiceCollection services,
        OutboxMessageOptions options)
        where TContext : DbContext, IOutboxMessageContext
        where TWorker : IOutboxMessageWorker
    {
        services.AddSingleton(options ?? new OutboxMessageOptions());

        if (!IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService(sp =>
            new OutboxMessageService(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<TWorker>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp.GetService<OutboxMessageOptions>()));
        }

        return services;
    }
}