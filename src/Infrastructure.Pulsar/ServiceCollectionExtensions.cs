// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Pulsar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    public static MessagingBuilderContext WithPulsarBroker(
        this MessagingBuilderContext context,
        PulsarMessageBrokerConfiguration configuration = null,
        string section = "Messaging:Pulsar")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<PulsarMessageBrokerConfiguration>() ?? new PulsarMessageBrokerConfiguration();

        context.Services.TryAddSingleton<IMessageBroker>(sp =>
        {
            var broker = new PulsarMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .Behaviors(sp.GetServices<IMessagePublisherBehavior>())
                .Behaviors(sp.GetServices<IMessageHandlerBehavior>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ServiceUrl(configuration.ServiceUrl)
                .Subscription(configuration.Subscription)
                .ProcessDelay(configuration.ProcessDelay));

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