// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure Queue Storage broker transport for the current queueing builder using a fluent options builder.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="optionsBuilder">The options builder used to configure the broker.</param>
    /// <returns>The queueing builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueing(builder.Configuration)
    ///     .WithSubscription&lt;OrderQueuedMessage, OrderQueuedHandler&gt;()
    ///     .WithAzureQueueStorageBroker(o => o
    ///         .ConnectionString("UseDevelopmentStorage=true")
    ///         .QueueNamePrefix("bit")
    ///         .AutoCreateQueue(true)
    ///         .MaxConcurrentCalls(8));
    /// </code>
    /// </example>
    public static QueueingBuilderContext WithAzureQueueStorageBroker(
        this QueueingBuilderContext context,
        Builder<AzureQueueStorageQueueBrokerOptionsBuilder, AzureQueueStorageQueueBrokerOptions> optionsBuilder)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        context.Services.TryAddSingleton(sp => CreateOptions(sp, optionsBuilder));
        context.Services.TryAddSingleton<AzureQueueStorageQueueBroker>(sp =>
            new AzureQueueStorageQueueBroker(
                sp.GetRequiredService<AzureQueueStorageQueueBrokerOptions>(),
                sp.GetRequiredService<QueueBrokerControlState>()));
        context.Services.TryAddSingleton<AzureQueueStorageQueueBrokerService>(sp =>
            new AzureQueueStorageQueueBrokerService(
                sp.GetRequiredService<AzureQueueStorageQueueBroker>().Runtime,
                sp.GetRequiredService<AzureQueueStorageQueueBrokerOptions>(),
                sp.GetRequiredService<QueueingRegistrationStore>(),
                sp.GetRequiredService<QueueBrokerControlState>()));
        context.Services.TryAddSingleton<IQueueBroker>(sp => sp.GetRequiredService<AzureQueueStorageQueueBroker>());
        context.Services.TryAddSingleton<IQueueBrokerService>(sp => sp.GetRequiredService<AzureQueueStorageQueueBrokerService>());

        return context;
    }

    /// <summary>
    /// Registers the Azure Queue Storage broker transport for the current queueing builder using configuration values.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="configuration">Optional configuration values.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The queueing builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueing(builder.Configuration)
    ///     .WithSubscription&lt;OrderQueuedMessage, OrderQueuedHandler&gt;()
    ///     .WithAzureQueueStorageBroker();
    /// </code>
    /// </example>
    public static QueueingBuilderContext WithAzureQueueStorageBroker(
        this QueueingBuilderContext context,
        AzureQueueStorageQueueBrokerConfiguration configuration = null,
        string section = "Queueing:AzureQueueStorage")
    {
        EnsureArg.IsNotNull(context, nameof(context));

        configuration ??= context.Configuration?.GetSection(section)?.Get<AzureQueueStorageQueueBrokerConfiguration>() ??
            new AzureQueueStorageQueueBrokerConfiguration();

        return context.WithAzureQueueStorageBroker(options => options
            .ConnectionString(configuration.ConnectionString)
            .QueueNamePrefix(configuration.QueueNamePrefix)
            .QueueNameSuffix(configuration.QueueNameSuffix)
            .AutoCreateQueue(configuration.AutoCreateQueue ?? true)
            .MaxConcurrentCalls(configuration.MaxConcurrentCalls ?? 1)
            .VisibilityTimeout(configuration.VisibilityTimeout ?? TimeSpan.FromSeconds(30))
            .PollingInterval(configuration.PollingInterval ?? TimeSpan.FromSeconds(1))
            .RetryDelay(configuration.RetryDelay)
            .MessageExpiration(configuration.MessageExpiration)
            .MaxDeliveryAttempts(configuration.MaxDeliveryAttempts ?? 5)
            .ProcessDelay(configuration.ProcessDelay ?? 0));
    }

    private static AzureQueueStorageQueueBrokerOptions CreateOptions(
        IServiceProvider serviceProvider,
        Builder<AzureQueueStorageQueueBrokerOptionsBuilder, AzureQueueStorageQueueBrokerOptions> optionsBuilder)
    {
        var options = optionsBuilder?.Invoke(new AzureQueueStorageQueueBrokerOptionsBuilder()).Build() ??
            new AzureQueueStorageQueueBrokerOptions();

        options.LoggerFactory ??= serviceProvider.GetRequiredService<ILoggerFactory>();
        options.EnqueuerBehaviors ??= serviceProvider.GetServices<IQueueEnqueuerBehavior>();
        options.HandlerBehaviors ??= serviceProvider.GetServices<IQueueHandlerBehavior>();
        options.HandlerFactory ??= new ServiceProviderQueueMessageHandlerFactory(serviceProvider);
        options.Serializer ??= new SystemTextJsonSerializer();

        return options;
    }
}
