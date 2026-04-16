namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;
using Configuration;

/// <summary>
/// Provides registration helpers for the Entity Framework backed queue broker transport.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Entity Framework backed queue broker transport for the current queueing builder using a fluent options builder.
    /// </summary>
    public static QueueingBuilderContext WithEntityFrameworkBroker<TContext>(
        this QueueingBuilderContext context,
        Builder<EntityFrameworkQueueBrokerOptionsBuilder, EntityFrameworkQueueBrokerOptions> optionsBuilder)
        where TContext : DbContext, IQueueingContext
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(context.Services, nameof(context.Services));

        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<EntityFrameworkQueueBrokerOptions>(context.Services, sp => CreateOptions(sp, optionsBuilder));
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<EntityFrameworkQueueBroker<TContext>>(context.Services, sp =>
            new EntityFrameworkQueueBroker<TContext>(
                sp,
                sp.GetRequiredService<EntityFrameworkQueueBrokerOptions>(),
                sp.GetRequiredService<QueueBrokerControlState>()));
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<EntityFrameworkQueueBrokerService<TContext>>(context.Services, sp =>
            new EntityFrameworkQueueBrokerService<TContext>(
                sp,
                sp.GetRequiredService<EntityFrameworkQueueBrokerOptions>(),
                sp.GetRequiredService<QueueingRegistrationStore>(),
                sp.GetRequiredService<QueueBrokerControlState>()));
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<IQueueBroker>(context.Services, sp => sp.GetRequiredService<EntityFrameworkQueueBroker<TContext>>());
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<IQueueBrokerService>(context.Services, sp => sp.GetRequiredService<EntityFrameworkQueueBrokerService<TContext>>());
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<EntityFrameworkQueueBrokerWorker<TContext>>(context.Services, sp =>
            new EntityFrameworkQueueBrokerWorker<TContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp,
                sp.GetRequiredService<EntityFrameworkQueueBroker<TContext>>(),
                sp.GetRequiredService<EntityFrameworkQueueBrokerOptions>()));
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<IQueueBrokerBackgroundProcessor>(context.Services, sp =>
            new EntityFrameworkQueueBrokerBackgroundProcessor<TContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<EntityFrameworkQueueBrokerOptions>(),
                ct => sp.GetRequiredService<EntityFrameworkQueueBrokerWorker<TContext>>().ProcessAsync(ct)));

        return context;
    }

    /// <summary>
    /// Registers the Entity Framework backed queue broker transport for the current queueing builder using configuration values.
    /// </summary>
    public static QueueingBuilderContext WithEntityFrameworkBroker<TContext>(
        this QueueingBuilderContext context,
        EntityFrameworkQueueBrokerConfiguration configuration = null,
        string section = "Queueing:EntityFramework")
        where TContext : DbContext, IQueueingContext
    {
        EnsureArg.IsNotNull(context, nameof(context));

        configuration ??= context.Configuration?.GetSection(section)?.Get<EntityFrameworkQueueBrokerConfiguration>() ??
            new EntityFrameworkQueueBrokerConfiguration();

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
                .AutoArchiveStatuses(configuration.AutoArchiveStatuses)
            .QueueNamePrefix(configuration.QueueNamePrefix)
            .QueueNameSuffix(configuration.QueueNameSuffix));
    }

    private static EntityFrameworkQueueBrokerOptions CreateOptions(
        IServiceProvider serviceProvider,
        Builder<EntityFrameworkQueueBrokerOptionsBuilder, EntityFrameworkQueueBrokerOptions> optionsBuilder)
    {
        var options = optionsBuilder?.Invoke(new EntityFrameworkQueueBrokerOptionsBuilder()).Build() ??
            new EntityFrameworkQueueBrokerOptions();

        options.LoggerFactory ??= serviceProvider.GetRequiredService<ILoggerFactory>();
        options.EnqueuerBehaviors ??= serviceProvider.GetServices<IQueueEnqueuerBehavior>();
        options.HandlerBehaviors ??= serviceProvider.GetServices<IQueueHandlerBehavior>();
        options.HandlerFactory ??= new ServiceProviderQueueMessageHandlerFactory(serviceProvider);
        options.Serializer ??= new SystemTextJsonSerializer();

        return options;
    }
}