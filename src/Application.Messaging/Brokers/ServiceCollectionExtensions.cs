// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using Configuration;
using Extensions;

/// <summary>
/// Represents service collection extensions.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Executes the with in process broker operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="section">The section used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static MessagingBuilderContext WithInProcessBroker(
        this MessagingBuilderContext context,
        InProcessMessageBrokerConfiguration configuration = null,
        string section = "Messaging:InProcess")
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        configuration ??= context.Configuration?.GetSection(section)?.Get<InProcessMessageBrokerConfiguration>() ??
            new InProcessMessageBrokerConfiguration();

        context.Services.TryAddSingleton(sp =>
        {
            var broker = new InProcessMessageBroker(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .Behaviors(sp.GetServices<IMessagePublisherBehavior>())
                .Behaviors(sp.GetServices<IMessageHandlerBehavior>())
                .HandlerFactory(new ServiceProviderMessageHandlerFactory(sp))
                .Serializer(new SystemTextJsonSerializer())
                .ProcessDelay(configuration.ProcessDelay)
                .MessageExpiration(configuration.MessageExpiration));

            foreach (var subscription in ServiceCollectionMessagingExtensions.GetSubscriptions())
            {
                broker.Subscribe(subscription.message, subscription.handler).AnyContext();
            }

            return broker;
        });
        context.Services.TryAddSingleton<IMessageBrokerRuntime>(sp => sp.GetRequiredService<InProcessMessageBroker>());
        context.Services.TryAddSingleton<IMessageBroker>(sp => sp.GetRequiredService<InProcessMessageBroker>());

        return context;
    }
}
