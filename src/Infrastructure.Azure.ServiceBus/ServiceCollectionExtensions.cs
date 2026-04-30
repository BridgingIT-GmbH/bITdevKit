// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Messaging;
using Common;
using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static MessagingBuilderContext WithServiceBusBroker(
        this MessagingBuilderContext context,
        ServiceBusMessageBrokerConfiguration configuration = null,
        string section = "Messaging:ServiceBus")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<ServiceBusMessageBrokerConfiguration>() ??
            new ServiceBusMessageBrokerConfiguration();

        context.Services.TryAddSingleton<IMessageBroker>(sp =>
        {
            var broker = new ServiceBusMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .Behaviors(sp.GetServices<IMessagePublisherBehavior>())
                .Behaviors(sp.GetServices<IMessageHandlerBehavior>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ConnectionString(configuration.ConnectionString)
                .ProcessDelay(configuration.ProcessDelay)
                .MessageExpiration(configuration.MessageExpiration));

            foreach (var (message, handler) in ServiceCollectionMessagingExtensions.Subscriptions)
            {
                broker.Subscribe(message, handler).AnyContext();
            }

            //foreach (var sub in broker.Subscriptions.GetAll())
            //{
            //    logger.LogInformation("{LogKey} ----- subscription: {MessageName}", Constants.LogKey, sub.Key);

            //    foreach (var subdetails in sub.Value.SafeNull())
            //    {
            //        logger.LogInformation("{LogKey} ----- subscription: {MessageName} -> {MessageHandler} ", Constants.LogKey, sub.Key, subdetails.HandlerType.Name);
            //    }
            //}

            return broker;
        });

        return context;
    }

    /// <summary>
    /// Registers the Azure Service Bus queue broker transport for the current queueing builder using a fluent options builder.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="optionsBuilder">The options builder used to configure the broker.</param>
    /// <returns>The queueing builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueing(builder.Configuration)
    ///     .WithSubscription<OrderQueuedMessage, OrderQueuedHandler>()
    ///     .WithServiceBusBroker(o => o
    ///         .ConnectionString("Endpoint=sb://...")
    ///         .QueueNamePrefix("bit")
    ///         .AutoCreateQueue(true)
    ///         .MaxConcurrentCalls(8));
    /// </code>
    /// </example>
    public static QueueingBuilderContext WithServiceBusBroker(
        this QueueingBuilderContext context,
        Builder<ServiceBusQueueBrokerOptionsBuilder, ServiceBusQueueBrokerOptions> optionsBuilder)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.TryAddSingleton(sp => CreateOptions(sp, optionsBuilder));
        context.Services.TryAddSingleton<ServiceBusQueueBroker>(sp =>
            new ServiceBusQueueBroker(
                sp.GetRequiredService<ServiceBusQueueBrokerOptions>(),
                sp.GetRequiredService<QueueBrokerControlState>()));
        context.Services.TryAddSingleton<ServiceBusQueueBrokerService>(sp =>
            new ServiceBusQueueBrokerService(
                sp.GetRequiredService<ServiceBusQueueBroker>().Runtime,
                sp.GetRequiredService<ServiceBusQueueBrokerOptions>(),
                sp.GetRequiredService<QueueingRegistrationStore>(),
                sp.GetRequiredService<QueueBrokerControlState>()));
        context.Services.TryAddSingleton<IQueueBroker>(sp => sp.GetRequiredService<ServiceBusQueueBroker>());
        context.Services.TryAddSingleton<IQueueBrokerService>(sp => sp.GetRequiredService<ServiceBusQueueBrokerService>());

        return context;
    }

    /// <summary>
    /// Registers the Azure Service Bus queue broker transport for the current queueing builder using configuration values.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="configuration">Optional configuration values.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The queueing builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueing(builder.Configuration)
    ///     .WithSubscription<OrderQueuedMessage, OrderQueuedHandler>()
    ///     .WithServiceBusBroker();
    /// </code>
    /// </example>
    public static QueueingBuilderContext WithServiceBusBroker(
        this QueueingBuilderContext context,
        ServiceBusQueueBrokerConfiguration configuration = null,
        string section = "Queueing:ServiceBus")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        configuration ??= context.Configuration?.GetSection(section)?.Get<ServiceBusQueueBrokerConfiguration>() ??
            new ServiceBusQueueBrokerConfiguration();

        return context.WithServiceBusBroker(options => options
            .ConnectionString(configuration.ConnectionString)
            .QueueNamePrefix(configuration.QueueNamePrefix)
            .QueueNameSuffix(configuration.QueueNameSuffix)
            .MaxConcurrentCalls(configuration.MaxConcurrentCalls)
            .PrefetchCount(configuration.PrefetchCount)
            .AutoCreateQueue(configuration.AutoCreateQueue)
            .MessageExpiration(configuration.MessageExpiration)
            .MaxDeliveryAttempts(configuration.MaxDeliveryAttempts)
            .ProcessDelay(configuration.ProcessDelay));
    }

    private static ServiceBusQueueBrokerOptions CreateOptions(
        IServiceProvider serviceProvider,
        Builder<ServiceBusQueueBrokerOptionsBuilder, ServiceBusQueueBrokerOptions> optionsBuilder)
    {
        var options = optionsBuilder?.Invoke(new ServiceBusQueueBrokerOptionsBuilder()).Build() ??
            new ServiceBusQueueBrokerOptions();

        options.LoggerFactory ??= serviceProvider.GetRequiredService<ILoggerFactory>();
        options.EnqueuerBehaviors ??= serviceProvider.GetServices<IQueueEnqueuerBehavior>();
        options.HandlerBehaviors ??= serviceProvider.GetServices<IQueueHandlerBehavior>();
        options.HandlerFactory ??= new ServiceProviderQueueMessageHandlerFactory(serviceProvider);
        options.Serializer ??= new SystemTextJsonSerializer();

        return options;
    }
}