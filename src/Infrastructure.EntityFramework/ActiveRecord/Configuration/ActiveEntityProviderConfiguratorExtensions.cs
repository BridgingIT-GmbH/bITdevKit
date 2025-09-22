// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Humanizer.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public static class ActiveEntityProviderConfiguratorExtensions
{
    /// <summary>
    /// Configures the Entity Framework provider for the entity using default options, inferring entity and ID types from the configurator.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// cfg.For&lt;Customer, CustomerId&gt;()
    ///     .UseEntityFrameworkProvider&lt;ActiveRecordDbContext&gt;()
    ///     .AddLoggingBehavior&lt;Customer, CustomerId&gt;();
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> UseEntityFrameworkProvider<TEntity, TId, TContext>(
        this ActiveEntityConfigurator<TEntity, TId> configurator)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.Services.AddSingleton(sp => new EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>(sp.GetRequiredService<ILoggerFactory>()));
        return configurator.UseProviderFactory(sp => new EntityFrameworkActiveEntityProvider<TEntity, TId, TContext>(
            sp.GetRequiredService<TContext>(),
            sp.GetRequiredService<EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>>()));
    }

    /// <summary>
    /// Configures the Entity Framework provider for the entity with custom options, inferring entity and ID types from the configurator.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <param name="optionsBuilder">The builder for Entity Framework provider options.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// cfg.For&lt;Order, OrderId&gt;()
    ///     .UseEntityFrameworkProvider&lt;ActiveRecordDbContext&gt;(o => o
    ///         .EnableOptimisticConcurrency()
    ///         .MergeStrategy(OrderMergeStrategy))
    ///     .AddDomainEventPublishingBehavior&lt;Order, OrderId&gt;();
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> UseEntityFrameworkProvider<TEntity, TId, TContext>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Func<EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>, EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>> optionsBuilder)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var options = optionsBuilder(new EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>()).Build();
        configurator.Services.AddSingleton(sp => options);
        return configurator.UseProviderFactory(sp => new EntityFrameworkActiveEntityProvider<TEntity, TId, TContext>(
            sp.GetRequiredService<TContext>(),
            sp.GetRequiredService<EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>>()));
    }

    /// <summary>
    /// Configures the Entity Framework provider for the entity using a builder to specify the DbContext type.
    /// The DbContext must be pre-registered in the service collection (e.g., via AddDbContext).
    /// </summary>
    /// <typeparam name="TEntity">The entity type, inferred from the configurator.</typeparam>
    /// <typeparam name="TId">The type of the entity's identifier, inferred from the configurator.</typeparam>
    /// <param name="configurator">The entity configurator.</param>
    /// <param name="builder">A configuration action for the Entity Framework provider builder.</param>
    /// <returns>The configurator instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;ActiveRecordDbContext&gt;(options => options.UseSqlServer(connectionString));
    /// services.AddActiveRecord(cfg =>
    /// {
    ///     cfg.For&lt;Customer, CustomerId&gt;()
    ///         .UseEntityFrameworkProvider(o => o.UseContext&lt;ActiveRecordDbContext&gt;())
    ///         .AddLoggingBehavior&lt;Customer, CustomerId&gt;();
    /// });
    /// </code>
    /// </example>
    public static ActiveEntityConfigurator<TEntity, TId> UseEntityFrameworkProvider<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Action<ActiveRecordEntityFrameworkProviderBuilder<TEntity, TId>> builder)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(builder);

        var providerBuilder = new ActiveRecordEntityFrameworkProviderBuilder<TEntity, TId>(configurator.Services);
        builder(providerBuilder);
        providerBuilder.Build(configurator);

        return configurator;
    }
}

/// <summary>
/// A builder class for configuring the Entity Framework provider without managing DbContext registration.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <remarks>
/// Initializes a new instance of the ActiveRecordEntityFrameworkProviderBuilder class.
/// </remarks>
/// <param name="services">The service collection for dependency injection.</param>
public class ActiveRecordEntityFrameworkProviderBuilder<TEntity, TId>(IServiceCollection services)
    where TEntity : class, IEntity
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private Type contextType;
    private Func<IServiceProvider, object> optionsFactory;
    private object options;

    /// <summary>
    /// Specifies the DbContext type to use for the Entity Framework provider.
    /// The DbContext must be pre-registered in the service collection.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// .UseEntityFrameworkProvider(o => o.UseContext&lt;ActiveRecordDbContext&gt;())
    /// </code>
    /// </example>
    public ActiveRecordEntityFrameworkProviderBuilder<TEntity, TId> Context<TContext>()
        where TContext : DbContext
    {
        this.contextType = typeof(TContext);

        return this;
    }

    /// <summary>
    /// Specifies custom options for the Entity Framework provider.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type, must be pre-registered.</typeparam>
    /// <param name="optionsFactory">A factory function to create provider options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// .UseEntityFrameworkProvider(o => o
    ///     .UseContext&lt;ActiveRecordDbContext&gt;()
    ///     .WithOptions&lt;ActiveRecordDbContext&gt;(sp => new ActiveRecordEntityFrameworkProviderOptions&lt;ActiveRecordDbContext, Customer&gt;(
    ///         sp.GetRequiredService&lt;ILoggerFactory&gt;()) { EnableOptimisticConcurrency = true }))
    /// </code>
    /// </example>
    public ActiveRecordEntityFrameworkProviderBuilder<TEntity, TId> Options<TContext>(Func<IServiceProvider, EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>> optionsFactory)
        where TContext : DbContext
    {
        //if (this.contextType == null || this.contextType != typeof(TContext))
        //{
        //    throw new InvalidOperationException("Context type must be set with UseContext<TContext> before WithOptions.");
        //}

        this.contextType = typeof(TContext);
        this.optionsFactory = sp => optionsFactory(sp) ?? throw new ArgumentNullException(nameof(optionsFactory));

        return this;
    }

    public ActiveRecordEntityFrameworkProviderBuilder<TEntity, TId> Options<TContext>(Builder<EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>, EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>> optionsBuilder)
        where TContext : DbContext
    {
        //if (this.contextType == null || this.contextType != typeof(TContext))
        //{
        //    throw new InvalidOperationException("Context type must be set with UseContext<TContext> before WithOptions.");
        //}

        this.contextType = typeof(TContext);
        this.options = optionsBuilder(new EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>()).Build();

        return this;
    }

    /// <summary>
    /// Builds the Entity Framework provider configuration and registers the provider factory with the configurator.
    /// </summary>
    /// <param name="configurator">The entity configurator to update with the provider factory.</param>
    /// <exception cref="InvalidOperationException">Thrown if the DbContext type is not specified.</exception>
    internal void Build(ActiveEntityConfigurator<TEntity, TId> configurator)
    {
        if (this.contextType == null)
        {
            throw new InvalidOperationException("DbContext type must be specified by using Context<TContext>.");
        }

        var options = this.options ??
            this.optionsFactory?.Invoke(this.services.BuildServiceProvider()) ??
            this.CreateDefaultOptions(this.contextType, this.services.BuildServiceProvider().GetRequiredService<ILoggerFactory>());

        configurator.Services.AddSingleton(sp => options);

        configurator.UseProviderFactory(sp =>
        {
            var context = sp.GetRequiredService(this.contextType) as DbContext;
            if (context == null)
            {
                throw new InvalidOperationException($"DbContext of type {this.contextType.Name} is not registered in the service collection.");
            }

            return (IActiveEntityEntityProvider<TEntity, TId>)ActivatorUtilities.CreateInstance(
                sp, typeof(EntityFrameworkActiveEntityProvider<,,>).MakeGenericType(typeof(TEntity), typeof(TId), this.contextType),
                [context, options]);
        });
    }

    private object CreateDefaultOptions(Type contextType, ILoggerFactory loggerFactory)
    {
        return Activator.CreateInstance(typeof(EntityFrameworkActiveEntityProviderOptions<,>).MakeGenericType(contextType, typeof(TEntity)), loggerFactory);
    }
}