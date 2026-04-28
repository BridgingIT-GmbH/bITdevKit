// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure;

using Application.Messaging;
using Application.Queueing;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static MessagingBuilderContext WithRabbitMQBroker(
        this MessagingBuilderContext context,
        RabbitMQMessageBrokerConfiguration configuration = null,
        string section = "Messaging:RabbitMQ")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<RabbitMQMessageBrokerConfiguration>() ??
            new RabbitMQMessageBrokerConfiguration();

        context.Services.TryAddSingleton<IMessageBroker>(sp =>
        {
            var broker = new RabbitMQMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .Behaviors(sp.GetServices<IMessagePublisherBehavior>())
                .Behaviors(sp.GetServices<IMessageHandlerBehavior>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .HostName(configuration.HostName)
                .ConnectionString(configuration.ConnectionString)
                .ExchangeName(configuration.ExchangeName)
                .QueueName(configuration.QueueName)
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
    ///     Registers the RabbitMQ queue broker transport for the current queueing builder using a fluent options builder.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="optionsBuilder">The options builder used to configure the broker.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext WithRabbitMQBroker(
        this QueueingBuilderContext context,
        Builder<RabbitMQQueueBrokerOptionsBuilder, RabbitMQQueueBrokerOptions> optionsBuilder)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.TryAddSingleton(sp =>
        {
            var options = optionsBuilder?.Invoke(new RabbitMQQueueBrokerOptionsBuilder()).Build() ??
                new RabbitMQQueueBrokerOptions();

            options.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();
            options.EnqueuerBehaviors ??= sp.GetServices<IQueueEnqueuerBehavior>();
            options.HandlerBehaviors ??= sp.GetServices<IQueueHandlerBehavior>();
            options.HandlerFactory ??= new ServiceProviderQueueMessageHandlerFactory(sp);
            options.Serializer ??= new SystemTextJsonSerializer();

            return options;
        });

        context.Services.TryAddSingleton<RabbitMQQueueBrokerService>();
        context.Services.TryAddSingleton<RabbitMQQueueBroker>();
        context.Services.TryAddSingleton<IQueueBroker>(sp => sp.GetRequiredService<RabbitMQQueueBroker>());
        context.Services.TryAddSingleton<IQueueBrokerService>(sp => sp.GetRequiredService<RabbitMQQueueBrokerService>());

        return context;
    }

    /// <summary>
    ///     Registers the RabbitMQ queue broker transport for the current queueing builder using configuration values.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="configuration">Optional configuration values.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext WithRabbitMQBroker(
        this QueueingBuilderContext context,
        RabbitMQQueueBrokerConfiguration configuration = null,
        string section = "Queueing:RabbitMQ")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        configuration ??= context.Configuration?.GetSection(section)?.Get<RabbitMQQueueBrokerConfiguration>() ??
            new RabbitMQQueueBrokerConfiguration();

        return context.WithRabbitMQBroker(options => options
            .HostName(configuration.HostName)
            .ConnectionString(configuration.ConnectionString)
            .QueueNamePrefix(configuration.QueueNamePrefix)
            .QueueNameSuffix(configuration.QueueNameSuffix)
            .PrefetchCount(configuration.PrefetchCount ?? 20)
            .DurableEnabled(configuration.IsDurable ?? true)
            .AutoDeleteQueueEnabled(configuration.AutoDeleteQueue ?? false)
            .ExclusiveQueueEnabled(configuration.ExclusiveQueue ?? false)
            .MessageExpiration(configuration.MessageExpiration)
            .MaxDeliveryAttempts(configuration.MaxDeliveryAttempts ?? 5)
            .ProcessDelay(configuration.ProcessDelay ?? 0));
    }
}