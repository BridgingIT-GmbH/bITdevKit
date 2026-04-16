namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Queueing;
using Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides dependency injection registration helpers for the queueing feature.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing(context =>
///     context.WithSubscription&lt;OrderQueuedMessage, OrderQueuedHandler&gt;())
///   .WithInProcessBroker();
/// </code>
/// </example>
public static class ServiceCollectionQueueingExtensions
{
    /// <summary>
    /// Adds queueing with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">An optional builder action.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext AddQueueing(
        this IServiceCollection services,
        Action<QueueingBuilderContext> optionsAction = null)
    {
        return services.AddQueueing(null, options: null, optionsAction);
    }

    /// <summary>
    /// Adds queueing using configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="optionsAction">An optional builder action.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext AddQueueing(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<QueueingBuilderContext> optionsAction = null)
    {
        return services.AddQueueing(configuration, options: null, optionsAction);
    }

    /// <summary>
    /// Adds queueing using a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The queueing options builder.</param>
    /// <param name="optionsAction">An optional builder action.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext AddQueueing(
        this IServiceCollection services,
        Builder<QueueingOptionsBuilder, QueueingOptions> optionsBuilder,
        Action<QueueingBuilderContext> optionsAction = null)
    {
        return services.AddQueueing(null,
            optionsBuilder is null ? null : optionsBuilder(new QueueingOptionsBuilder()).Build(),
            optionsAction);
    }

    /// <summary>
    /// Adds queueing using configuration and a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="optionsBuilder">The queueing options builder.</param>
    /// <param name="optionsAction">An optional builder action.</param>
    /// <returns>The queueing builder context.</returns>
    public static QueueingBuilderContext AddQueueing(
        this IServiceCollection services,
        IConfiguration configuration,
        Builder<QueueingOptionsBuilder, QueueingOptions> optionsBuilder,
        Action<QueueingBuilderContext> optionsAction = null)
    {
        return services.AddQueueing(configuration,
            optionsBuilder is null ? null : optionsBuilder(new QueueingOptionsBuilder()).Build(),
            optionsAction);
    }

    /// <summary>
    /// Adds queueing and returns a shared builder context.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="options">The queueing options.</param>
    /// <param name="optionsAction">An optional builder action.</param>
    /// <returns>The queueing builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueing(configuration, new QueueingOptions { StartupDelay = TimeSpan.FromSeconds(3) }, context =>
    /// {
    ///     context.WithSubscription&lt;OrderQueuedMessage, OrderQueuedHandler&gt;();
    /// });
    /// </code>
    /// </example>
    public static QueueingBuilderContext AddQueueing(
        this IServiceCollection services,
        IConfiguration configuration,
        QueueingOptions options,
        Action<QueueingBuilderContext> optionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var registrationStore = GetOrAddSingleton(services, _ => new QueueingRegistrationStore());
        var contextOptions = GetOrAddSingleton(services, _ => new QueueingOptions());
        GetOrAddSingleton(services, _ => new QueueBrokerControlState());
        MergeOptions(contextOptions, options);

        services.Scan(scan =>
            scan.FromApplicationDependencies(a => !a.FullName.MatchAny(Blacklists.ApplicationDependencies))
                .AddClasses(classes => classes.AssignableTo(typeof(IQueueMessageHandler<>)), true));

        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<IQueueSubscriptionMap, QueueSubscriptionMap>(services);

        if (!IsBuildTimeOpenApiGeneration())
        {
            services.AddHostedService<QueueingService>();
        }

        var context = new QueueingBuilderContext(services, configuration, contextOptions, registrationStore);
        optionsAction?.Invoke(context);

        return context;
    }

    /// <summary>
    /// Adds a typed queue subscription.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="context">The queueing builder context.</param>
    /// <returns>The queueing builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddQueueing()
    ///     .WithSubscription&lt;OrderQueuedMessage, OrderQueuedHandler&gt;();
    /// </code>
    /// </example>
    public static QueueingBuilderContext WithSubscription<TMessage, THandler>(this QueueingBuilderContext context)
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>
    {
        context.RegistrationStore.Add(typeof(TMessage), typeof(THandler));
        return context;
    }

    /// <summary>
    /// Adds an enqueuer behavior.
    /// </summary>
    public static QueueingBuilderContext WithBehavior<TBehavior>(
        this QueueingBuilderContext context,
        IQueueEnqueuerBehavior behavior = null)
        where TBehavior : class, IQueueEnqueuerBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IQueueEnqueuerBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IQueueEnqueuerBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Adds an enqueuer behavior using a factory.
    /// </summary>
    public static QueueingBuilderContext WithBehavior(
        this QueueingBuilderContext context,
        Func<IServiceProvider, IQueueEnqueuerBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IQueueEnqueuerBehavior), implementationFactory);
        }

        return context;
    }

    /// <summary>
    /// Adds an enqueuer behavior instance.
    /// </summary>
    public static QueueingBuilderContext WithBehavior(
        this QueueingBuilderContext context,
        IQueueEnqueuerBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IQueueEnqueuerBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Adds a handler behavior.
    /// </summary>
    public static QueueingBuilderContext WithBehavior<TBehavior>(
        this QueueingBuilderContext context,
        IQueueHandlerBehavior behavior = null)
        where TBehavior : class, IQueueHandlerBehavior
    {
        if (behavior is null)
        {
            context.Services.AddSingleton<IQueueHandlerBehavior, TBehavior>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IQueueHandlerBehavior), behavior);
        }

        return context;
    }

    /// <summary>
    /// Adds a handler behavior using a factory.
    /// </summary>
    public static QueueingBuilderContext WithBehavior(
        this QueueingBuilderContext context,
        Func<IServiceProvider, IQueueHandlerBehavior> implementationFactory)
    {
        if (implementationFactory is not null)
        {
            context.Services.AddSingleton(typeof(IQueueHandlerBehavior), implementationFactory);
        }

        return context;
    }

    /// <summary>
    /// Adds a handler behavior instance.
    /// </summary>
    public static QueueingBuilderContext WithBehavior(
        this QueueingBuilderContext context,
        IQueueHandlerBehavior behavior)
    {
        if (behavior is not null)
        {
            context.Services.AddSingleton(typeof(IQueueHandlerBehavior), behavior);
        }

        return context;
    }

    private static T GetOrAddSingleton<T>(IServiceCollection services, Func<IServiceCollection, T> factory)
        where T : class
    {
        var descriptor = services.FirstOrDefault(item => item.ServiceType == typeof(T) && item.Lifetime == ServiceLifetime.Singleton);
        if (descriptor?.ImplementationInstance is T instance)
        {
            return instance;
        }

        var created = factory(services);
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, created);

        return created;
    }

    private static void MergeOptions(QueueingOptions target, QueueingOptions source)
    {
        if (source is null)
        {
            return;
        }

        target.Enabled = source.Enabled;
        if (source.StartupDelay > TimeSpan.Zero)
        {
            target.StartupDelay = source.StartupDelay;
        }
    }

    private static bool IsBuildTimeOpenApiGeneration()
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }

}
