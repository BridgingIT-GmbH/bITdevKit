// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Scrutor;

public class RepositoryBuilderContext<TEntity>
    where TEntity : class, IEntity
{
    private readonly List<Action<IServiceCollection>> behaviors = new();
    private ServiceDescriptor repositoryDescriptor;

    public RepositoryBuilderContext(IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped, IConfiguration configuration = null)
    {
        this.Services = services;
        this.Lifetime = lifetime;
        this.Configuration = configuration;
    }

    public IServiceCollection Services { get; }

    public ServiceLifetime Lifetime { get; }

    public IConfiguration Configuration { get; }

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

    public RepositoryBuilderContext<TEntity> WithTransactions<TTransactions>(Func<IServiceProvider, TTransactions> implementationFactory)
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

    public RepositoryBuilderContext<TEntity> WithBehavior<TBehavior>()
        where TBehavior : class, IGenericRepository<TEntity>
    {
        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    public RepositoryBuilderContext<TEntity> WithBehavior2<TBehavior>()
        where TBehavior : class, IGenericRepository<TEntity>
    {
        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    public RepositoryBuilderContext<TEntity> WithBehavior<TBehavior>(Func<IGenericRepository<TEntity>, TBehavior> behavior)
        where TBehavior : notnull, IGenericRepository<TEntity>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>>((service, _) => behavior(service)));
        this.RegisterBehaviors();

        return this;
    }

    public RepositoryBuilderContext<TEntity> WithBehavior<TBehavior>(Func<IGenericRepository<TEntity>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IGenericRepository<TEntity>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IGenericRepository<TEntity>>((inner, sp) => behavior(inner, sp)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Registers all recorded behaviors (decorators). Before registering all existing behavior registrations are removed.
    /// This needs to be done to apply the registrations in reverse order.
    /// </summary>
    private IServiceCollection RegisterBehaviors()
    {
        // reset the repo registration to the original implementation, as scrutor changes the implementation
        this.repositoryDescriptor ??= this.Services.Find<IGenericRepository<TEntity>>();
        if (this.repositoryDescriptor is null)
        {
            throw new Exception($"Cannot register behaviors for {typeof(IGenericRepository<TEntity>).PrettyName()} as it has not been registerd.");
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

        foreach (var descriptor in this.Services.Where(s => s.ServiceType is DecoratedType && s.ServiceType.ImplementsInterface(typeof(IGenericRepository<TEntity>)))?.ToList())
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