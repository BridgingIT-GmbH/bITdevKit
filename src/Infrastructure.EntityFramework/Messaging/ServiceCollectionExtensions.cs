// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;
using Configuration;
using Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides registration helpers for the Entity Framework backed message broker transport.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Entity Framework backed message broker transport for the current messaging builder using a fluent options builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IMessagingContext"/>.</typeparam>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="optionsBuilder">The fluent options builder used to customize broker runtime options.</param>
    /// <returns>The current <see cref="MessagingBuilderContext"/> for further composition.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddMessaging(builder.Configuration)
    ///     .WithSubscription&lt;OrderSubmittedMessage, OrderSubmittedHandler&gt;()
    ///     .WithEntityFrameworkBroker&lt;AppDbContext&gt;(options => options
    ///         .ProcessingInterval(TimeSpan.FromSeconds(10))
    ///         .LeaseDuration(TimeSpan.FromSeconds(30))
    ///         .MaxDeliveryAttempts(5)
    ///         .MessageExpiration(TimeSpan.FromHours(1)));
    /// </code>
    /// </example>
    public static MessagingBuilderContext WithEntityFrameworkBroker<TContext>(
        this MessagingBuilderContext context,
        Builder<EntityFrameworkMessageBrokerOptionsBuilder, EntityFrameworkMessageBrokerOptions> optionsBuilder)
        where TContext : DbContext, IMessagingContext
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.TryAddSingleton(sp => CreateOptions(sp, optionsBuilder));
        context.Services.TryAddSingleton(sp => CreateBroker<TContext>(sp));
        context.Services.TryAddSingleton<IMessageBroker>(sp => sp.GetRequiredService<EntityFrameworkMessageBroker<TContext>>());
        context.Services.TryAddSingleton<IMessageBrokerService, EntityFrameworkMessageBrokerStoreService<TContext>>();
        context.Services.TryAddSingleton<EntityFrameworkMessageBrokerWorker<TContext>>(sp =>
            new EntityFrameworkMessageBrokerWorker<TContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp,
                sp.GetRequiredService<EntityFrameworkMessageBroker<TContext>>(),
                sp.GetRequiredService<EntityFrameworkMessageBrokerOptions>()));

        if (!IsBuildTimeOpenApiGeneration())
        {
            context.Services.AddHostedService(sp =>
                new EntityFrameworkMessageBrokerService(
                    sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<IHostApplicationLifetime>(),
                    sp.GetRequiredService<EntityFrameworkMessageBrokerOptions>(),
                    ct => sp.GetRequiredService<EntityFrameworkMessageBrokerWorker<TContext>>().ProcessAsync(ct)));
        }

        return context;
    }

    /// <summary>
    /// Registers the Entity Framework backed message broker transport for the current messaging builder using configuration values.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IMessagingContext"/>.</typeparam>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="configuration">Optional broker configuration. When omitted, values are loaded from configuration.</param>
    /// <param name="section">The configuration section used to bind broker settings.</param>
    /// <returns>The current <see cref="MessagingBuilderContext"/> for further composition.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddMessaging(builder.Configuration)
    ///     .WithEntityFrameworkBroker&lt;AppDbContext&gt;(
    ///         new EntityFrameworkMessageBrokerConfiguration
    ///         {
    ///             ProcessingInterval = TimeSpan.FromSeconds(10),
    ///             LeaseDuration = TimeSpan.FromSeconds(30),
    ///             MaxDeliveryAttempts = 5,
    ///             MessageExpiration = TimeSpan.FromHours(1)
    ///         });
    /// </code>
    /// </example>
    public static MessagingBuilderContext WithEntityFrameworkBroker<TContext>(
        this MessagingBuilderContext context,
        EntityFrameworkMessageBrokerConfiguration configuration = null,
        string section = "Messaging:EntityFramework")
        where TContext : DbContext, IMessagingContext
    {
        EnsureArg.IsNotNull(context, nameof(context));

        configuration ??= context.Configuration?.GetSection(section)?.Get<EntityFrameworkMessageBrokerConfiguration>() ??
            new EntityFrameworkMessageBrokerConfiguration();

        return context.WithEntityFrameworkBroker<TContext>(options => options
            .Enabled(configuration.Enabled)
            .AutoSave(configuration.AutoSave)
            .StartupDelay(configuration.StartupDelay)
            .ProcessingInterval(configuration.ProcessingInterval)
            .ProcessingDelay(configuration.ProcessingDelay)
            .ProcessingCount(configuration.ProcessingCount)
            .MaxDeliveryAttempts(configuration.MaxDeliveryAttempts)
            .LeaseDuration(configuration.LeaseDuration)
            .LeaseRenewalInterval(configuration.LeaseRenewalInterval)
            .MessageExpiration(configuration.MessageExpiration)
                .AutoArchiveAfter(configuration.AutoArchiveAfter)
                .AutoArchiveStatuses(configuration.AutoArchiveStatuses));
    }

    private static EntityFrameworkMessageBrokerOptions CreateOptions(
        IServiceProvider serviceProvider,
        Builder<EntityFrameworkMessageBrokerOptionsBuilder, EntityFrameworkMessageBrokerOptions> optionsBuilder)
    {
        var options = optionsBuilder?.Invoke(new EntityFrameworkMessageBrokerOptionsBuilder()).Build() ??
            new EntityFrameworkMessageBrokerOptions();

        options.LoggerFactory ??= serviceProvider.GetRequiredService<ILoggerFactory>();
        options.PublisherBehaviors ??= serviceProvider.GetServices<IMessagePublisherBehavior>();
        options.HandlerBehaviors ??= serviceProvider.GetServices<IMessageHandlerBehavior>();
        options.HandlerFactory ??= new ServiceProviderMessageHandlerFactory(serviceProvider);
        options.Serializer ??= new SystemTextJsonSerializer();

        return options;
    }

    private static EntityFrameworkMessageBroker<TContext> CreateBroker<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext, IMessagingContext
    {
        var broker = new EntityFrameworkMessageBroker<TContext>(
            serviceProvider,
            serviceProvider.GetRequiredService<EntityFrameworkMessageBrokerOptions>());

        foreach (var subscription in ServiceCollectionMessagingExtensions.Subscriptions)
        {
            broker.Subscribe(subscription.message, subscription.handler).AnyContext();
        }

        return broker;
    }
}