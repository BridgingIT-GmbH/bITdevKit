// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Active Entity configuration for multiple entities using a fluent configuration builder.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The configuration action for Active Entities.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddActiveEntity(cfg =>
    /// {
    ///     cfg.For&lt;Customer, CustomerId&gt;()
    ///         .UseEntityFrameworkProvider&lt;AppDbContext&gt;()
    ///         .AddLoggingBehavior();
    ///     cfg.For&lt;Order, OrderId&gt;()
    ///         .UseInMemoryProvider()
    ///         .AddDomainEventPublishingBehavior();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddActiveEntity(this IServiceCollection services, Action<ActiveEntityConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ActiveEntityConfigurationBuilder(services);
        configure(builder);
        builder.Register();
        return services;
    }

    /// <summary>
    /// Adds Active Entity configuration for a specific entity type, returning a builder for further customization.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>An ActiveEntityBuilder for fluent configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddActiveEntity&lt;Customer, CustomerId&gt;()
    ///     .WithInMemoryProvider()
    ///     .WithBehavior&lt;ActiveEntityEntityLoggingBehavior&lt;Customer&gt;&gt;()
    ///     .Build();
    /// </code>
    /// </example>
    public static ActiveEntityBuilder<TEntity, TId> AddActiveEntity<TEntity, TId>(this IServiceCollection services)
        where TEntity : class, IEntity
    {
        return services == null
            ? throw new ArgumentNullException(nameof(services))
            : new ActiveEntityBuilder<TEntity, TId>(services);
    }
}