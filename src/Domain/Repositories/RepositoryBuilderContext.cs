// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Configuration;
using Scrutor;

/// <summary>
///     The <c>RepositoryBuilderContext</c> class provides an abstraction for configuring
///     repository services within the dependency injection framework.
/// </summary>
/// <typeparam name="TEntity">The type of the entity that this repository will manage.</typeparam>
public class RepositoryBuilderContext<TEntity>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null)
    where TEntity : class, IEntity
{
    private readonly List<Action<IServiceCollection>> behaviors = [];

    private ServiceDescriptor repositoryDescriptor;

    /// <summary>
    ///     Gets the collection of service descriptors where repositories can be registered.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    ///     Gets the lifetime of the services to be registered in the dependency injection container.
    /// </summary>
    /// <remarks>
    ///     This property determines the <see cref="ServiceLifetime" /> for services and components
    ///     added to the service collection. Possible values include Scoped, Singleton, and Transient.
    /// </remarks>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    ///     Gets the configuration instance to be used in the repository context.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    ///     Configures the repository context to use the specified transaction type for handling transactions.
    /// </summary>
    /// <typeparam name="TTransactions">The type of the transaction to be used.</typeparam>
    /// <returns>The current <see cref="RepositoryBuilderContext{TEntity}" /> instance to allow further configuration.</returns>
    public RepositoryBuilderContext<TEntity> WithTransactions<TTransactions>()
        where TTransactions : class, IRepositoryTransaction<TEntity>
    {
        switch (this.Lifetime)
        {
            case ServiceLifetime.Singleton:
                this.Services.AddSingleton<IRepositoryTransaction<TEntity>, TTransactions>();

                break;
            case ServiceLifetime.Transient:
                this.Services.AddTransient<IRepositoryTransaction<TEntity>, TTransactions>();

                break;
            default:
                this.Services.AddScoped<IRepositoryTransaction<TEntity>, TTransactions>();

                break;
        }

        return this;
    }

    /// <summary>
    ///     Registers the specified transaction implementation for the repository context.
    /// </summary>
    /// <typeparam name="TTransactions">The type of the transaction implementation to register.</typeparam>
    /// <param name="implementationFactory">A factory method that returns an instance of the transaction implementation.</param>
    /// <returns>The updated repository builder context with the registered transaction implementation.</returns>
    public RepositoryBuilderContext<TEntity> WithTransactions<TTransactions>(
        Func<IServiceProvider, TTransactions> implementationFactory)
        where TTransactions : class, IRepositoryTransaction<TEntity>
    {
        EnsureArg.IsNotNull(implementationFactory, nameof(implementationFactory));

        switch (this.Lifetime)
        {
            case ServiceLifetime.Singleton:
                this.Services.AddSingleton<IRepositoryTransaction<TEntity>>(implementationFactory);

                break;
            case ServiceLifetime.Transient:
                this.Services.AddTransient<IRepositoryTransaction<TEntity>>(implementationFactory);

                break;
            default:
                this.Services.AddScoped<IRepositoryTransaction<TEntity>>(implementationFactory);

                break;
        }

        return this;
    }

    /// <summary>
    ///     Applies a specified behavior to the repository context. This allows the integration of custom repository behaviors
    ///     such as logging, tracing, or domain event handling by decorating the repository with the provided behavior.
    /// </summary>
    /// <typeparam name="TBehavior">
    ///     The type of the behavior to be applied, which must implement IGenericRepository for the
    ///     specific entity type.
    /// </typeparam>
    /// <returns>The current instance of <see cref="RepositoryBuilderContext{TEntity}" /> to allow method chaining.</returns>
    public RepositoryBuilderContext<TEntity> WithBehavior<TBehavior>()
        where TBehavior : class, IGenericRepository<TEntity>
    {
        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Adds a behavior to the repository by decorating the generic repository with the specified behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to add.</typeparam>
    /// <returns>The current <see cref="RepositoryBuilderContext{TEntity}" /> with the added behavior.</returns>
    public RepositoryBuilderContext<TEntity> WithBehavior2<TBehavior>()
        where TBehavior : class, IGenericRepository<TEntity>
    {
        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Adds a behavior to the repository by decorating the generic repository with the provided behavior.
    /// </summary>
    /// <param name="behavior">A function that takes an instance of IGenericRepository and returns an instance of TBehavior.</param>
    /// <typeparam name="TBehavior">The type of behavior that implements IGenericRepository and is not null.</typeparam>
    /// <returns>The current instance of RepositoryBuilderContext with the added behavior.</returns>
    public RepositoryBuilderContext<TEntity> WithBehavior<TBehavior>(
        Func<IGenericRepository<TEntity>, TBehavior> behavior)
        where TBehavior : notnull, IGenericRepository<TEntity>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>>((service, _) => behavior(service)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Registers a behavior for the generic repository. The behavior is applied using the specified factory method.
    /// </summary>
    /// <typeparam name="TBehavior">The type of the behavior to be added.</typeparam>
    /// <param name="behavior">A factory method that creates the behavior, given the inner repository and service provider.</param>
    /// <returns>The current instance of <see cref="RepositoryBuilderContext{TEntity}" />.</returns>
    public RepositoryBuilderContext<TEntity> WithBehavior<TBehavior>(
        Func<IGenericRepository<TEntity>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IGenericRepository<TEntity>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>>((inner, sp) => behavior(inner, sp)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Registers all recorded behaviors (decorators). Before registering, all existing behavior
    ///     registrations are removed to apply the registrations in reverse order.
    /// </summary>
    /// <returns>The updated IServiceCollection with the registered behaviors.</returns>
    private IServiceCollection RegisterBehaviors()
    {
        // reset the repo registration to the original implementation, as scrutor changes the implementation
        this.repositoryDescriptor ??= this.Services.Find<IGenericRepository<TEntity>>();
        if (this.repositoryDescriptor is null)
        {
            throw new Exception(
                $"Cannot register behaviors for {typeof(IGenericRepository<TEntity>).PrettyName()} as it has not been registerd.");
        }

        var descriptorIndex = this.Services.IndexOf<IGenericRepository<TEntity>>();
        if (descriptorIndex != -1)
        {
            this.Services[descriptorIndex] = this.repositoryDescriptor;
        }
        else
        {
            return this.Services;
        }

        foreach (var descriptor in this.Services.Where(s =>
                         s.ServiceType is DecoratedType &&
                         s.ServiceType.ImplementsInterface(typeof(IGenericRepository<TEntity>)))
                     .ToList())
        {
            this.Services.Remove(descriptor); // remove the registered behavior
        }

        // register all behaviors in reverse order (first...last)
        foreach (var behavior in this.behaviors.AsEnumerable().Reverse())
        {
            behavior.Invoke(this.Services);
        }

        return this.Services;
    }
}