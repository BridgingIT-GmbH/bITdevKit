namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.Azure.ServiceBus;
using BridgingIT.DevKit.Common;
using Configuration;
using Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides registration helpers for the Azure Service Bus queue broker transport.
/// </summary>
public static partial class ServiceCollectionExtensions
{
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
