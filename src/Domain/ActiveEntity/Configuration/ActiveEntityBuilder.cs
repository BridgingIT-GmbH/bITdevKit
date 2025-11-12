// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Initializes a new instance of the ActiveEntityBuilder class.
/// </summary>
/// <param name="services">The service collection for dependency injection.</param>
/// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// var builder = new ActiveEntityBuilder&lt;Customer, CustomerId&gt;(services);
/// </code>
/// </example>
public class ActiveEntityBuilder<TEntity, TId>(IServiceCollection services)
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets the service collection for dependency injection.
    /// </summary>
    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Gets the entity configurator for this builder.
    /// </summary>
    public ActiveEntityConfigurator<TEntity, TId> Configurator { get; } = new ActiveEntityConfigurator<TEntity, TId>(services);

    /// <summary>
    /// Sets the provider factory for the entity.
    /// </summary>
    /// <param name="factory">A function that creates the entity provider using the service provider.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.WithProvider(sp => new ActiveEntityInMemoryProvider&lt;Customer, CustomerId&gt;(
    ///     sp.GetRequiredService&lt;ILoggerFactory&gt;(),
    ///     new ActiveEntityInMemoryProviderOptions&lt;Customer&gt; { Context = new InMemoryContext&lt;Customer&gt;() }));
    /// </code>
    /// </example>
    public ActiveEntityBuilder<TEntity, TId> WithProvider(Func<IServiceProvider, IActiveEntityEntityProvider<TEntity, TId>> factory)
    {
        this.Configurator.UseProviderFactory(sp => factory(sp));
        return this;
    }

    /// <summary>
    /// Adds a behavior for the entity with optional configuration options.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to add.</typeparam>
    /// <param name="options">Optional configuration options for the behavior.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.WithBehavior&lt;ActiveEntityEntityLoggingBehavior&lt;Customer&gt;&gt;();
    /// builder.WithBehavior&lt;ActiveEntityDomainEventPublishingBehavior&lt;Customer, CustomerId&gt;&gt;(
    ///     new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });
    /// </code>
    /// </example>
    public ActiveEntityBuilder<TEntity, TId> WithBehavior<TBehavior>(object options = null)
        where TBehavior : IActiveEntityBehavior<TEntity>
    {
        this.Configurator.AddBehaviorType(typeof(TBehavior), options);
        return this;
    }

    /// <summary>
    /// Builds the configuration and returns the service collection.
    /// </summary>
    /// <returns>The service collection for further configuration.</returns>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// var builder = new ActiveEntityBuilder&lt;Order, OrderId&gt;(services)
    ///     .WithInMemoryProvider()
    ///     .WithBehavior&lt;ActiveEntityEntityLoggingBehavior&lt;Order&gt;&gt;();
    /// var configuredServices = builder.Build();
    /// </code>
    /// </example>
    public IServiceCollection Build()
    {
        return this.Services;
    }
}