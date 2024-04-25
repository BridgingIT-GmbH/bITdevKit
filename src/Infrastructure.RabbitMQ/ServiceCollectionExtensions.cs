// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    public static MessagingBuilderContext WithRabbitMQBroker(
        this MessagingBuilderContext context,
        RabbitMQMessageBrokerConfiguration configuration = null,
        string section = "Messaging:RabbitMQ")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<RabbitMQMessageBrokerConfiguration>() ?? new RabbitMQMessageBrokerConfiguration();

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
}