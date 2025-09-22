// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Provides a fluent API for configuring multiple Active Entity entities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ActiveEntityConfigurationBuilder class.
/// </remarks>
/// <param name="services">The service collection for dependency injection.</param>
/// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// var builder = new ActiveEntityConfigurationBuilder(services);
/// builder.For&lt;Customer, CustomerId&gt;();
/// </code>
/// </example>
public class ActiveEntityConfigurationBuilder(IServiceCollection services)
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly List<Action<IServiceCollection>> registrations = [];

    /// <summary>
    /// Configures Active Entity for a specific entity type with its identifier type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type, must inherit from ActiveEntity&lt;TEntity, TId&gt;.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier.</typeparam>
    /// <param name="configure">Optional configuration action for the entity.</param>
    /// <returns>The entity configurator for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// builder.For&lt;Customer, CustomerId&gt;(cfg => cfg
    ///     .UseEntityFrameworkProvider&lt;AppDbContext&gt;()
    ///     .AddLoggingBehavior());
    /// </code>
    /// </example>
    public ActiveEntityConfigurator<TEntity, TId> For<TEntity, TId>(Action<ActiveEntityConfigurator<TEntity, TId>> configure = null)
        where TEntity : ActiveEntity<TEntity, TId>, IEntity
    {
        var configurator = new ActiveEntityConfigurator<TEntity, TId>(this.services, this.registrations);
        configure?.Invoke(configurator);

        return configurator;
    }

    /// <summary>
    /// Configures Active Entity for an entity type, inferring its identifier type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type, must inherit from ActiveEntity&lt;TEntity, TId&gt;.</typeparam>
    /// <param name="configure">Optional configuration action for the entity.</param>
    /// <returns>The entity configurator for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if TEntity does not inherit from ActiveEntity&lt;TEntity, TId&gt;.</exception>
    /// <example>
    /// <code>
    /// builder.For&lt;Order&gt;(cfg => cfg
    ///     .UseInMemoryProvider()
    ///     .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false }));
    /// </code>
    /// </example>
    public IActiveEntityConfigurator For<TEntity>(Action<IActiveEntityConfigurator> configure = null)
        where TEntity : class, IEntity
    {
        var type = typeof(TEntity);
        var baseType = type.BaseType;

        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ActiveEntity<,>))
            {
                var genericArgs = baseType.GetGenericArguments();
                if (genericArgs.Length == 2 && genericArgs[0] == type)
                {
                    var tIdType = genericArgs[1];
                    var method = this.GetType().GetMethod(nameof(For), BindingFlags.Public | BindingFlags.Instance)
                        .MakeGenericMethod(type, tIdType);
                    var configurator = (IActiveEntityConfigurator)method.Invoke(this, [null]);
                    configure?.Invoke(configurator);

                    return configurator;
                }
            }

            baseType = baseType.BaseType;
        }

        throw new InvalidOperationException($"Type {typeof(TEntity).Name} must inherit from ActiveEntity<TEntity, TId>.");
    }

    /// <summary>
    /// Registers all configured services with the service collection.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = new ActiveEntityConfigurationBuilder(services);
    /// builder.For&lt;Customer, CustomerId&gt;().UseEntityFrameworkProvider&lt;AppDbContext&gt;();
    /// builder.Register();
    /// </code>
    /// </example>
    internal void Register()
    {
        foreach (var registration in this.registrations)
        {
            registration(this.services);
        }
    }
}