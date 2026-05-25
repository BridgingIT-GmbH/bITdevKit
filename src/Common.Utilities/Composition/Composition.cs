// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Reflection;
using System.Runtime.ExceptionServices;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BridgingIT.DevKit.Common.Utilities.Composition;
/// <summary>
/// Adds the composition building blocks to dependency injection.
/// </summary>
public static class CompositionServiceCollectionExtensions
{
    /// <summary>
    /// Adds composition services and returns a fluent composition builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The composition builder.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    ///
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .Decorate(d =&gt; d.With&lt;LoggingWeatherClient&gt;())
    ///     .Intercept(i =&gt; i.WithLogging().WithTimeout(TimeSpan.FromSeconds(5)))
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    public static ICompositionBuilder AddComposition(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var state = services.Find<CompositionRegistrationState>()?.ImplementationInstance as CompositionRegistrationState;
        if (state is null)
        {
            state = new CompositionRegistrationState();
            services.AddSingleton(state);
        }

        if (!services.Any(d => d.ServiceType == typeof(IAdapterFactory)))
        {
            services.AddSingleton<IAdapterFactory, AdapterFactory>();
        }

        return new CompositionBuilder(services, state);
    }
}

/// <summary>
/// Represents the root composition builder.
/// </summary>
public interface ICompositionBuilder
{
    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Starts composition for a service contract.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <returns>A service composition builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;INotificationSender&gt;()
    ///     .Use&lt;EmailNotificationSender&gt;()
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    IServiceCompositionBuilder<TService> For<TService>()
        where TService : class;

    /// <summary>
    /// Starts adapter registration for a source contract or type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <returns>An adapter source builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .Adapt&lt;ThirdPartyWeatherClient&gt;()
    ///     .To&lt;IWeatherClient&gt;()
    ///     .Using&lt;ThirdPartyWeatherClientAdapter&gt;()
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    IAdapterSourceBuilder<TSource> Adapt<TSource>()
        where TSource : class;

    /// <summary>
    /// Starts keyed strategy registration for a service contract.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy contract.</typeparam>
    /// <returns>A strategy builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .Strategies&lt;IPaymentProvider&gt;()
    ///     .Add&lt;StripePaymentProvider&gt;("stripe")
    ///     .Add&lt;PaypalPaymentProvider&gt;("paypal")
    ///     .WithDefault("stripe");
    /// </code>
    /// </example>
    IStrategyBuilder<TStrategy> Strategies<TStrategy>()
        where TStrategy : class;

    /// <summary>
    /// Starts composite registration for a service contract.
    /// </summary>
    /// <typeparam name="TService">The service contract.</typeparam>
    /// <typeparam name="TComposite">The composite implementation.</typeparam>
    /// <param name="configure">The child implementation configuration.</param>
    /// <returns>A composite builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .Composite&lt;INotificationSender, CompositeNotificationSender&gt;(c =&gt; c
    ///         .With&lt;EmailNotificationSender&gt;()
    ///         .With&lt;TeamsNotificationSender&gt;())
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    ICompositeBuilder<TService, TComposite> Composite<TService, TComposite>(
        Action<ICompositeChildrenBuilder<TService>> configure)
        where TService : class
        where TComposite : class, TService;

    /// <summary>
    /// Starts chain registration for a handler contract and context.
    /// </summary>
    /// <typeparam name="THandler">The chain handler contract.</typeparam>
    /// <typeparam name="TContext">The chain context type.</typeparam>
    /// <param name="configure">The chain configuration.</param>
    /// <returns>A chain builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .Chain&lt;IFileImportHandler, FileImportContext&gt;(chain =&gt; chain
    ///         .With&lt;CsvImportHandler&gt;()
    ///         .With&lt;JsonImportHandler&gt;());
    /// </code>
    /// </example>
    IChainBuilder<THandler, TContext> Chain<THandler, TContext>(
        Action<IChainBuilder<THandler, TContext>> configure)
        where THandler : class, IChainHandler<TContext>;

}

/// <summary>
/// Represents a builder that starts a composed service registration.
/// </summary>
/// <typeparam name="TService">The service contract.</typeparam>
public interface IServiceCompositionBuilder<TService>
    where TService : class
{
    /// <summary>
    /// Uses a concrete implementation for the service contract.
    /// </summary>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <returns>A typed composition builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    IServiceCompositionBuilder<TService, TImplementation> Use<TImplementation>()
        where TImplementation : class, TService;

}

/// <summary>
/// Represents a builder that configures a composed service implementation.
/// </summary>
/// <typeparam name="TService">The service contract.</typeparam>
/// <typeparam name="TImplementation">The implementation type.</typeparam>
public interface IServiceCompositionBuilder<TService, TImplementation>
    where TService : class
    where TImplementation : class, TService
{
    /// <summary>
    /// Adds decorators to the service chain.
    /// </summary>
    /// <param name="configure">The decorator configuration.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .Decorate(d =&gt; d.With&lt;LoggingWeatherClient&gt;())
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    IServiceCompositionBuilder<TService, TImplementation> Decorate(
        Action<IDecoratorBuilder<TService>> configure);

    /// <summary>
    /// Adds interception to the service chain.
    /// </summary>
    /// <param name="configure">The interception configuration.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// services.AddComposition()
    ///     .For&lt;IWeatherClient&gt;()
    ///     .Use&lt;WeatherClient&gt;()
    ///     .Intercept(i =&gt; i.WithLogging().WithRetry(TimeSpan.FromSeconds(1)))
    ///     .RegisterScoped();
    /// </code>
    /// </example>
    IServiceCompositionBuilder<TService, TImplementation> Intercept(
        Action<IInterceptionBuilder<TService>> configure);

    /// <summary>
    /// Replaces existing registrations of the service contract when registering the composed service.
    /// </summary>
    /// <returns>The builder.</returns>
    IServiceCompositionBuilder<TService, TImplementation> ReplaceExisting();

    /// <summary>
    /// Only registers the composed service when the contract is not already registered.
    /// </summary>
    /// <returns>The builder.</returns>
    IServiceCompositionBuilder<TService, TImplementation> TryRegister();

    /// <summary>
    /// Appends an additional registration for the service contract.
    /// </summary>
    /// <returns>The builder.</returns>
    IServiceCompositionBuilder<TService, TImplementation> AddAdditional();

    /// <summary>
    /// Registers the composed service as a singleton.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterSingleton();

    /// <summary>
    /// Registers the composed service as scoped.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterScoped();

    /// <summary>
    /// Registers the composed service as transient.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterTransient();
}

/// <summary>
/// Represents a decorator builder for a service contract.
/// </summary>
/// <typeparam name="TService">The service contract.</typeparam>
public interface IDecoratorBuilder<TService>
    where TService : class
{
    /// <summary>
    /// Adds a decorator type to the chain.
    /// </summary>
    /// <typeparam name="TDecorator">The decorator type.</typeparam>
    /// <returns>The decorator builder.</returns>
    IDecoratorBuilder<TService> With<TDecorator>()
        where TDecorator : class, TService;
}

/// <summary>
/// Represents an interception builder for a service contract.
/// </summary>
/// <typeparam name="TService">The service contract.</typeparam>

internal enum CompositionRegistrationMode
{
    ReplaceExisting,
    TryRegister,
    AddAdditional
}

internal sealed class CompositionRegistrationState
{
    public IDictionary<(Type Source, Type Target), Type> AdapterMappings { get; } =
        new Dictionary<(Type Source, Type Target), Type>();
}

internal sealed class CompositionBuilder(IServiceCollection services, CompositionRegistrationState state) : ICompositionBuilder
{
    public IServiceCollection Services { get; } = services;

    public IServiceCompositionBuilder<TService> For<TService>()
        where TService : class
    {
        return new ServiceCompositionBuilder<TService>(this);
    }

    public IAdapterSourceBuilder<TSource> Adapt<TSource>()
        where TSource : class
    {
        return new AdapterSourceBuilder<TSource>(this, state);
    }

    public IStrategyBuilder<TStrategy> Strategies<TStrategy>()
        where TStrategy : class
    {
        var definition = this.Services.Find<StrategyDefinition<TStrategy>>()?.ImplementationInstance as StrategyDefinition<TStrategy>;
        if (definition is null)
        {
            definition = new StrategyDefinition<TStrategy>();
            this.Services.AddSingleton(definition);
        }

        EnsureRegistration(
            typeof(IStrategyResolver<TStrategy>),
            ServiceLifetime.Scoped,
            sp => new StrategyResolver<TStrategy>(sp, sp.GetRequiredService<StrategyDefinition<TStrategy>>()),
            CompositionRegistrationMode.ReplaceExisting);

        return new StrategyBuilder<TStrategy>(definition);
    }

    public ICompositeBuilder<TService, TComposite> Composite<TService, TComposite>(
        Action<ICompositeChildrenBuilder<TService>> configure)
        where TService : class
        where TComposite : class, TService
    {
        var builder = new CompositeBuilder<TService, TComposite>(this);
        configure?.Invoke(builder);
        return builder;
    }

    public IChainBuilder<THandler, TContext> Chain<THandler, TContext>(
        Action<IChainBuilder<THandler, TContext>> configure)
        where THandler : class, IChainHandler<TContext>
    {
        var definition = this.Services.Find<ChainDefinition<THandler, TContext>>()?.ImplementationInstance as ChainDefinition<THandler, TContext>;
        if (definition is null)
        {
            definition = new ChainDefinition<THandler, TContext>();
            this.Services.AddSingleton(definition);
        }

        var builder = new ChainBuilder<THandler, TContext>(this, definition);
        configure?.Invoke(builder);
        builder.EnsureDefaultRegistration();
        return builder;
    }

    internal void ApplyRegistration(Type serviceType, ServiceLifetime lifetime, Func<IServiceProvider, object> factory, CompositionRegistrationMode mode)
    {
        var descriptor = new ServiceDescriptor(serviceType, sp => factory(sp), lifetime);
        ApplyRegistration(descriptor, mode);
    }

    internal void ApplyRegistration(ServiceDescriptor descriptor, CompositionRegistrationMode mode)
    {
        if (mode == CompositionRegistrationMode.ReplaceExisting)
        {
            foreach (var existing in this.Services.Where(d => d.ServiceType == descriptor.ServiceType).ToList())
            {
                this.Services.Remove(existing);
            }

            this.Services.Add(descriptor);
            return;
        }

        if (mode == CompositionRegistrationMode.TryRegister)
        {
            if (!this.Services.Any(d => d.ServiceType == descriptor.ServiceType))
            {
                this.Services.Add(descriptor);
            }

            return;
        }

        this.Services.Add(descriptor);
    }

    private void EnsureRegistration(Type serviceType, ServiceLifetime lifetime, Func<IServiceProvider, object> factory, CompositionRegistrationMode mode)
    {
        if (!this.Services.Any(d => d.ServiceType == serviceType))
        {
            this.ApplyRegistration(serviceType, lifetime, factory, mode);
        }
    }
}

internal sealed class ServiceCompositionBuilder<TService>(CompositionBuilder root) : IServiceCompositionBuilder<TService>
    where TService : class
{
    public IServiceCompositionBuilder<TService, TImplementation> Use<TImplementation>()
        where TImplementation : class, TService
    {
        return new ServiceCompositionBuilder<TService, TImplementation>(root);
    }

}

internal sealed class ServiceCompositionBuilder<TService, TImplementation>(CompositionBuilder root)
    : IServiceCompositionBuilder<TService, TImplementation>
    where TService : class
    where TImplementation : class, TService
{
    private readonly List<Type> decorators = [];
    private readonly List<Type> interceptors = [];
    private readonly List<Func<IServiceProvider, IInterceptionBehavior<TService>>> behaviorFactories = [];
    private CompositionRegistrationMode mode = CompositionRegistrationMode.ReplaceExisting;

    public IServiceCompositionBuilder<TService, TImplementation> Decorate(Action<IDecoratorBuilder<TService>> configure)
    {
        var builder = new DecoratorBuilder<TService>(this.decorators);
        configure?.Invoke(builder);
        return this;
    }

    public IServiceCompositionBuilder<TService, TImplementation> Intercept(Action<IInterceptionBuilder<TService>> configure)
    {
        var builder = new InterceptionBuilder<TService>(this.interceptors, this.behaviorFactories);
        configure?.Invoke(builder);
        return this;
    }

    public IServiceCompositionBuilder<TService, TImplementation> ReplaceExisting()
    {
        this.mode = CompositionRegistrationMode.ReplaceExisting;
        return this;
    }

    public IServiceCompositionBuilder<TService, TImplementation> TryRegister()
    {
        this.mode = CompositionRegistrationMode.TryRegister;
        return this;
    }

    public IServiceCompositionBuilder<TService, TImplementation> AddAdditional()
    {
        this.mode = CompositionRegistrationMode.AddAdditional;
        return this;
    }

    public ICompositionBuilder RegisterSingleton() => this.Register(ServiceLifetime.Singleton);

    public ICompositionBuilder RegisterScoped() => this.Register(ServiceLifetime.Scoped);

    public ICompositionBuilder RegisterTransient() => this.Register(ServiceLifetime.Transient);

    private ICompositionBuilder Register(ServiceLifetime lifetime)
    {
        var serviceType = typeof(TService);
        var implementationType = typeof(TImplementation);

        root.ApplyRegistration(
            new ServiceDescriptor(
                serviceType,
                sp => CompositionRuntime.BuildComposedService(sp, serviceType, implementationType, this.decorators, this.interceptors, this.behaviorFactories),
                lifetime),
            this.mode);

        return root;
    }
}

internal sealed class DecoratorBuilder<TService>(ICollection<Type> decorators) : IDecoratorBuilder<TService>
    where TService : class
{
    public IDecoratorBuilder<TService> With<TDecorator>()
        where TDecorator : class, TService
    {
        CompositionValidation.ValidateWrapper(typeof(TDecorator), typeof(TService), "Decorator");
        decorators.Add(typeof(TDecorator));
        return this;
    }
}


internal static class CompositionValidation
{
    public static void ValidateAssignable(Type implementationType, Type serviceType, string kind)
    {
        if (!serviceType.IsAssignableFrom(implementationType))
        {
            throw new InvalidOperationException(
                $"{kind} registration for {serviceType.Name} failed: {implementationType.Name} does not implement {serviceType.Name}.");
        }
    }

    public static void ValidateCreatable(Type implementationType, string kind)
    {
        if (implementationType.IsAbstract || implementationType.IsInterface)
        {
            throw new InvalidOperationException($"{kind} type {implementationType.Name} must be a concrete class.");
        }
    }

    public static void ValidateWrapper(Type wrapperType, Type serviceType, string kind)
    {
        ValidateAssignable(wrapperType, serviceType, kind);
        ValidateCreatable(wrapperType, kind);

        if (!wrapperType.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == serviceType)))
        {
            throw new InvalidOperationException(
                $"{kind} {wrapperType.Name} could not be constructed. Ensure it has a constructor accepting {serviceType.Name} or a compatible service chain parameter.");
        }
    }
}

internal static class CompositionRuntime
{
    public static object BuildComposedService<TService>(
        IServiceProvider services,
        Type serviceType,
        Type implementationType,
        IReadOnlyList<Type> decorators,
        IReadOnlyList<Type> interceptors,
        IReadOnlyList<Func<IServiceProvider, IInterceptionBehavior<TService>>> behaviorFactories)
        where TService : class
    {
        Func<TService> implementationFactory = () => (TService)ResolveOrCreate(implementationType, services);
        object current = behaviorFactories.Count > 0
            ? RuntimeInterceptionHostFactory.Create(services, implementationFactory, behaviorFactories)
            : implementationFactory();

        for (var i = interceptors.Count - 1; i >= 0; i--)
        {
            current = CreateWrapper(interceptors[i], serviceType, current, "Interceptor", services);
        }

        for (var i = decorators.Count - 1; i >= 0; i--)
        {
            current = CreateWrapper(decorators[i], serviceType, current, "Decorator", services);
        }

        return current;
    }

    public static object CreateWrapper(
        Type wrapperType,
        Type serviceType,
        object inner,
        string kind,
        IServiceProvider services)
    {
        try
        {
            return ActivatorUtilities.CreateInstance(services, wrapperType, inner);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{kind} {wrapperType.Name} could not be constructed. Ensure it has a constructor accepting {serviceType.Name} or a compatible service chain parameter.",
                ex);
        }
    }

    public static object ResolveOrCreate(Type type, IServiceProvider services)
    {
        return services.GetService(type) ?? ActivatorUtilities.CreateInstance(services, type);
    }
}
