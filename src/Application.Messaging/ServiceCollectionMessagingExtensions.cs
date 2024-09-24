// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Configuration;
using Extensions;
using Logging;

public static class ServiceCollectionMessagingExtensions
{
    public static readonly List<(Type message, Type handler)> Subscriptions = [];
    private static MessagingOptions contextOptions;

    public static MessagingBuilderContext AddMessaging(
        this IServiceCollection services,
        Action<MessagingBuilderContext> optionsAction = null)
    {
        return services.AddMessaging(null, options: null, optionsAction);
    }

    public static MessagingBuilderContext AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MessagingBuilderContext> optionsAction = null)
    {
        return services.AddMessaging(configuration, options: null, optionsAction);
    }

    public static MessagingBuilderContext AddMessaging(
        this IServiceCollection services,
        Builder<MessagingOptionsBuilder, MessagingOptions> optionsBuilder,
        Action<MessagingBuilderContext> optionsAction = null)
    {
        return services.AddMessaging(null,
            optionsBuilder is null ? null : optionsBuilder(new MessagingOptionsBuilder()).Build(),
            optionsAction);
    }

    public static MessagingBuilderContext AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Builder<MessagingOptionsBuilder, MessagingOptions> optionsBuilder,
        Action<MessagingBuilderContext> optionsAction = null)
    {
        return services.AddMessaging(configuration,
            optionsBuilder is null ? null : optionsBuilder(new MessagingOptionsBuilder()).Build(),
            optionsAction);
    }

    public static MessagingBuilderContext AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        MessagingOptions options,
        Action<MessagingBuilderContext> optionsAction = null)
    {
        contextOptions ??= options;

        services.Scan(scan =>
            scan // https://andrewlock.net/using-scrutor-to-automatically-register-your-services-with-the-asp-net-core-di-container/
                //.FromExecutingAssembly()
                .FromApplicationDependencies(a =>
                    !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }))
                .AddClasses(classes => classes.AssignableTo(typeof(IMessageHandler<>)), true));

        services.AddHostedService(sp => // should be scoped
            new MessagingService(sp.GetService<ILoggerFactory>(), sp, contextOptions));
        services.TryAddSingleton<ISubscriptionMap, SubscriptionMap>();

        optionsAction?.Invoke(new MessagingBuilderContext(services));

        return new MessagingBuilderContext(services, configuration, contextOptions);
    }

    [Obsolete("Please use WithSubscription")]
    public static MessagingBuilderContext Subscribe<TMessage, THandler>(this MessagingBuilderContext context)
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        context.WithSubscription<TMessage, THandler>();

        return context;
    }

    public static MessagingBuilderContext WithSubscription<TMessage, THandler>(this MessagingBuilderContext context)
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        Subscriptions.Add((typeof(TMessage), typeof(THandler)));

        return context;
    }

    public static MessagingBuilderContext WithBehavior<TBehavior>(
        this MessagingBuilderContext context,
        IMessagePublisherBehavior behavior = null)
        where TBehavior : class, IMessagePublisherBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IMessagePublisherBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IMessagePublisherBehavior), behavior);
        }

        return context;
    }

    public static MessagingBuilderContext WithBehavior(
        this MessagingBuilderContext context,
        Func<IServiceProvider, IMessagePublisherBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IMessagePublisherBehavior), implementationFactory);
        }

        return context;
    }

    public static MessagingBuilderContext WithBehavior(
        this MessagingBuilderContext context,
        IMessagePublisherBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IMessagePublisherBehavior), behavior);
        }

        return context;
    }

    public static MessagingBuilderContext WithBehavior<TBehavior>(
        this MessagingBuilderContext context,
        IMessageHandlerBehavior behavior = null)
        where TBehavior : class, IMessageHandlerBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IMessageHandlerBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IMessageHandlerBehavior), behavior);
        }

        return context;
    }

    public static MessagingBuilderContext WithBehavior(
        this MessagingBuilderContext context,
        Func<IServiceProvider, IMessageHandlerBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IMessageHandlerBehavior), implementationFactory);
        }

        return context;
    }

    public static MessagingBuilderContext WithBehavior(
        this MessagingBuilderContext context,
        IMessageHandlerBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IMessageHandlerBehavior), behavior);
        }

        return context;
    }
}