// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

public interface IActiveEntityConfigurator
{
    /// <summary>
    /// Gets the service collection for dependency injection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Sets the provider factory for the entity.
    /// </summary>
    /// <param name="factory">A function that creates the entity provider using the service provider.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.UseProviderFactory(sp => new ActiveEntityInMemoryProvider&lt;Customer, CustomerId&gt;(
    ///     sp.GetRequiredService&lt;ILoggerFactory&gt;(),
    ///     new ActiveEntityInMemoryProviderOptions&lt;Customer&gt; { Context = new InMemoryContext&lt;Customer&gt;() }));
    /// </code>
    /// </example>
    IActiveEntityConfigurator UseProviderFactory(Func<IServiceProvider, object> factory);

    /// <summary>
    /// Adds a behavior type for the entity with optional configuration options.
    /// </summary>
    /// <param name="behaviorType">The type of the behavior to add.</param>
    /// <param name="options">Optional configuration options for the behavior.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.AddBehaviorType(typeof(ActiveEntityEntityLoggingBehavior&lt;Order&gt;));
    /// configurator.AddBehaviorType(typeof(ActiveEntityDomainEventPublishingBehavior&lt;Order, OrderId&gt;),
    ///     new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });
    /// </code>
    /// </example>
    IActiveEntityConfigurator AddBehaviorType(Type behaviorType, object options = null);
}

/// <summary>
/// Initializes a new instance of the ActiveEntityEntityConfigurator class.
/// </summary>
/// <param name="services">The service collection for dependency injection.</param>
/// <param name="registrations">Optional list to collect service registrations.</param>
/// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// var registrations = new List&lt;Action&lt;IServiceCollection&gt;&gt;();
/// var configurator = new ActiveEntityEntityConfigurator&lt;Customer, CustomerId&gt;(services, registrations);
/// </code>
/// </example>
public class ActiveEntityConfigurator<TEntity, TId>(IServiceCollection services, List<Action<IServiceCollection>> registrations = null) : IActiveEntityConfigurator
    where TEntity : class, IEntity
{
    private Func<IServiceProvider, IActiveEntityEntityProvider<TEntity, TId>> providerFactory;
    private readonly List<(Type behaviorType, object options)> behaviors = [];
    private readonly List<Action<IServiceCollection>> registrations = registrations ?? [];

    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Sets the provider factory for the entity using a non-generic factory function.
    /// </summary>
    /// <param name="factory">A function that creates the entity provider using the service provider.</param>
    /// <returns>The configurator instance as an interface for non-generic chaining.</returns>
    /// <example>
    /// <code>
    /// configurator.UseProviderFactory(sp => new ActiveEntityInMemoryProvider&lt;Customer, CustomerId&gt;(
    ///     sp.GetRequiredService&lt;ILoggerFactory&gt;(),
    ///     new ActiveEntityInMemoryProviderOptions&lt;Customer&gt; { Context = new InMemoryContext&lt;Customer&gt;() }));
    /// </code>
    /// </example>
    public IActiveEntityConfigurator UseProviderFactory(Func<IServiceProvider, object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.providerFactory = sp => (IActiveEntityEntityProvider<TEntity, TId>)factory(sp);
        this.registrations.Add(services => services.AddScoped<IActiveEntityEntityProvider<TEntity, TId>>(sp => this.providerFactory(sp)));
        return this;
    }

    /// <summary>
    /// Sets the provider factory for the entity using a generic factory function.
    /// </summary>
    /// <param name="factory">A function that creates the entity provider using the service provider.</param>
    /// <returns>The configurator instance for fluent chaining with entity-specific methods.</returns>
    /// <example>
    /// <code>
    /// configurator.UseProviderFactory(sp => new ActiveEntityInMemoryProvider&lt;Order, OrderId&gt;(
    ///     sp.GetRequiredService&lt;ILoggerFactory&gt;(),
    ///     new ActiveEntityInMemoryProviderOptions&lt;Order&gt; { Context = new InMemoryContext&lt;Order&gt;() }))
    ///     .AddLoggingBehavior&lt;Order, OrderId&gt;();
    /// </code>
    /// </example>
    public ActiveEntityConfigurator<TEntity, TId> UseProviderFactory(Func<IServiceProvider, IActiveEntityEntityProvider<TEntity, TId>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.providerFactory = factory;
        this.registrations.Add(services => services.AddScoped<IActiveEntityEntityProvider<TEntity, TId>>(sp => this.providerFactory(sp)));
        return this;
    }

    public IActiveEntityConfigurator AddBehaviorType(Type behaviorType, object options = null)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        if (!typeof(IActiveEntityBehavior<TEntity>).IsAssignableFrom(behaviorType))
        {
            throw new ArgumentException($"Type {behaviorType.Name} does not implement IActiveEntityEntityBehavior<{typeof(TEntity).Name}>.");
        }

        this.behaviors.Add((behaviorType, options));
        this.registrations.Add(services => services.AddScoped(typeof(IActiveEntityBehavior<TEntity>), sp =>
            options != null
                ? ActivatorUtilities.CreateInstance(sp, behaviorType, options)
                : ActivatorUtilities.CreateInstance(sp, behaviorType)));

        return this;
    }

    /// <summary>
    /// Checks if a behavior of the specified type has already been added.
    /// </summary>
    public bool HasBehaviorType(Type behaviorType)
    {
        // implement this method to check if a behavior type is already added
        foreach (var (type, _) in this.behaviors)
        {
            if (type == behaviorType)
            {
                return true;
            }
        }

        return false;
    }
}