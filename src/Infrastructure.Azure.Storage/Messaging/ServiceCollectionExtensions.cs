// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure Queue Storage message broker transport for the current messaging builder using a fluent options builder.
    /// </summary>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="optionsBuilder">The options builder used to configure the broker.</param>
    /// <returns>The messaging builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddMessaging(builder.Configuration)
    ///     .WithSubscription&lt;OrderCreatedMessage, SendEmailHandler&gt;()
    ///     .WithAzureQueueStorageBroker(o => o
    ///         .ConnectionString("UseDevelopmentStorage=true")
    ///         .QueueNamePrefix("bit")
    ///         .AutoCreateQueue(true));
    /// </code>
    /// </example>
    public static MessagingBuilderContext WithAzureQueueStorageBroker(
        this MessagingBuilderContext context,
        Builder<AzureQueueStorageMessageBrokerOptionsBuilder, AzureQueueStorageMessageBrokerOptions> optionsBuilder)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.TryAddSingleton<IMessageBroker>(sp =>
        {
            var options = optionsBuilder?.Invoke(new AzureQueueStorageMessageBrokerOptionsBuilder()).Build() ??
                new AzureQueueStorageMessageBrokerOptions();

            options.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();
            options.PublisherBehaviors ??= sp.GetServices<IMessagePublisherBehavior>();
            options.HandlerBehaviors ??= sp.GetServices<IMessageHandlerBehavior>();
            options.HandlerFactory ??= new ServiceProviderMessageHandlerFactory(sp);
            options.Serializer ??= new SystemTextJsonSerializer();

            var broker = new AzureQueueStorageMessageBroker(options);

            foreach (var subscription in ServiceCollectionMessagingExtensions.Subscriptions)
            {
                broker.Subscribe(subscription.message, subscription.handler).AnyContext();
            }

            return broker;
        });

        return context;
    }

    /// <summary>
    /// Registers the Azure Queue Storage message broker transport for the current messaging builder using configuration values.
    /// </summary>
    /// <param name="context">The messaging builder context.</param>
    /// <param name="configuration">Optional configuration values.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The messaging builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddMessaging(builder.Configuration)
    ///     .WithSubscription&lt;OrderCreatedMessage, SendEmailHandler&gt;()
    ///     .WithAzureQueueStorageBroker();
    /// </code>
    /// </example>
    public static MessagingBuilderContext WithAzureQueueStorageBroker(
        this MessagingBuilderContext context,
        AzureQueueStorageMessageBrokerConfiguration configuration = null,
        string section = "Messaging:AzureQueueStorage")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        configuration ??= context.Configuration?.GetSection(section)?.Get<AzureQueueStorageMessageBrokerConfiguration>() ??
            new AzureQueueStorageMessageBrokerConfiguration();

        return context.WithAzureQueueStorageBroker(options => options
            .ConnectionString(configuration.ConnectionString)
            .QueueNamePrefix(configuration.QueueNamePrefix)
            .QueueNameSuffix(configuration.QueueNameSuffix)
            .AutoCreateQueue(configuration.AutoCreateQueue ?? true)
            .MaxConcurrentCalls(configuration.MaxConcurrentCalls ?? 1)
            .VisibilityTimeout(configuration.VisibilityTimeout ?? TimeSpan.FromSeconds(30))
            .PollingInterval(configuration.PollingInterval ?? TimeSpan.FromSeconds(1))
            .MessageExpiration(configuration.MessageExpiration)
            .ProcessDelay(configuration.ProcessDelay ?? 0));
    }
}
