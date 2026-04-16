namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using Configuration;
using Extensions;

/// <summary>
/// Provides registration helpers for queue broker implementations in the application layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process queue broker using a fluent options builder.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="optionsBuilder">The options builder used to configure the broker.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext WithInProcessBroker(
        this QueueingBuilderContext context,
        Builder<InProcessQueueBrokerOptionsBuilder, InProcessQueueBrokerOptions> optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.TryAddSingleton(sp =>
        {
            var options = optionsBuilder?.Invoke(new InProcessQueueBrokerOptionsBuilder()).Build() ?? new InProcessQueueBrokerOptions();
            options.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();
            options.EnqueuerBehaviors ??= sp.GetServices<IQueueEnqueuerBehavior>();
            options.HandlerBehaviors ??= sp.GetServices<IQueueHandlerBehavior>();
            options.HandlerFactory ??= new ServiceProviderQueueMessageHandlerFactory(sp);
            options.Serializer ??= new SystemTextJsonSerializer();

            return options;
        });

        context.Services.TryAddSingleton<InProcessQueueBrokerRuntime>();
        context.Services.TryAddSingleton<InProcessQueueBroker>();
        context.Services.TryAddSingleton<InProcessQueueBrokerService>();
        context.Services.TryAddSingleton<IQueueBroker>(sp => sp.GetRequiredService<InProcessQueueBroker>());
        context.Services.TryAddSingleton<IQueueBrokerService>(sp => sp.GetRequiredService<InProcessQueueBrokerService>());

        return context;
    }

    /// <summary>
    /// Registers the in-process queue broker using configuration values.
    /// </summary>
    /// <param name="context">The queueing builder context.</param>
    /// <param name="configuration">Optional configuration values.</param>
    /// <param name="section">The configuration section name.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext WithInProcessBroker(
        this QueueingBuilderContext context,
        InProcessQueueBrokerConfiguration configuration = null,
        string section = "Queueing:InProcess")
    {
        ArgumentNullException.ThrowIfNull(context);

        configuration ??= context.Configuration?.GetSection(section)?.Get<InProcessQueueBrokerConfiguration>() ??
            new InProcessQueueBrokerConfiguration();

        return context.WithInProcessBroker(options => options
            .ProcessDelay(configuration.ProcessDelay)
            .MessageExpiration(configuration.MessageExpiration)
            .QueueNamePrefix(configuration.QueueNamePrefix)
            .QueueNameSuffix(configuration.QueueNameSuffix)
            .MaxDegreeOfParallelism(configuration.MaxDegreeOfParallelism)
            .EnsureOrdered(configuration.EnsureOrdered));
    }
}