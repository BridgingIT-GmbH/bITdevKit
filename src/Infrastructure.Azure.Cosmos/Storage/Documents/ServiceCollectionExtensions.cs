// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Azure.Cosmos;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure;
using BridgingIT.DevKit.Infrastructure.Azure.Storage;
using Configuration;
using Extensions;
using Logging;
using Scrutor;

public static partial class ServiceCollectionExtensions
{
    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddCosmosDocumentStoreClient<T>(o => o, lifetime);
    }

    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        CosmosClient cosmosClient,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(cosmosClient, nameof(cosmosClient));

        return services.AddCosmosDocumentStoreClient<T>(o => o.Client(cosmosClient), lifetime);
    }

    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        Builder<CosmosSqlProviderOptionsBuilder<CosmosStorageDocument>, CosmosSqlProviderOptions<CosmosStorageDocument>>
            providerOptionsBuilder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(providerOptionsBuilder, nameof(providerOptionsBuilder));

        // ensure some default options values
        var providerOptions = providerOptionsBuilder(new CosmosSqlProviderOptionsBuilder<CosmosStorageDocument>())
            .Container("storage_documents")
            .PartitionKey(e => e.Type)
            .Build();

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<IDocumentStoreClient<T>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();

                    return new DocumentStoreClient<T>(
                        new CosmosDocumentStoreProvider(new CosmosSqlProvider<CosmosStorageDocument>(providerOptions)));
                });

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<IDocumentStoreClient<T>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();

                    return new DocumentStoreClient<T>(
                        new CosmosDocumentStoreProvider(new CosmosSqlProvider<CosmosStorageDocument>(providerOptions)));
                });

                break;
            default:
                services.TryAddScoped<IDocumentStoreClient<T>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();

                    return new DocumentStoreClient<T>(
                        new CosmosDocumentStoreProvider(new CosmosSqlProvider<CosmosStorageDocument>(providerOptions)));
                });

                break;
        }

        return new CosmosDocumentStoreClientBuilderContext<T>(services, lifetime);
    }

    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        CosmosDocumentStoreProvider provider,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(provider, nameof(provider));

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider));

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider));

                break;
            default:
                services.TryAddScoped<IDocumentStoreClient<T>>(sp => new DocumentStoreClient<T>(provider));

                break;
        }

        return new CosmosDocumentStoreClientBuilderContext<T>(services, lifetime);
    }
}

public class CosmosDocumentStoreClientBuilderContext<T>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null)
    where T : class, new()
{
    private readonly List<Action<IServiceCollection>> behaviors = [];
    private ServiceDescriptor repositoryDescriptor;

    public IServiceCollection Services { get; } = services;

    public ServiceLifetime Lifetime { get; } = lifetime;

    public IConfiguration Configuration { get; } = configuration;

    public CosmosDocumentStoreClientBuilderContext<T> WithBehavior<TBehavior>()
        where TBehavior : class, IDocumentStoreClient<T>
    {
        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    public CosmosDocumentStoreClientBuilderContext<T> WithBehavior<TBehavior>(
        Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((service, _) => behavior(service)));
        this.RegisterBehaviors();

        return this;
    }

    public CosmosDocumentStoreClientBuilderContext<T> WithBehavior<TBehavior>(
        Func<IDocumentStoreClient<T>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((inner, sp) => behavior(inner, sp)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Registers all recorded behaviors (decorators). Before registering all existing behavior registrations are removed.
    ///     This needs to be done to apply the registrations in reverse order.
    /// </summary>
    private IServiceCollection RegisterBehaviors()
    {
        // reset the repo registration to the original implementation, as scrutor changes the implementation
        this.repositoryDescriptor ??= this.Services.Find<IDocumentStoreClient<T>>();
        if (this.repositoryDescriptor is null)
        {
            throw new Exception(
                $"Cannot register behaviors for {typeof(IDocumentStoreClient<T>).PrettyName()} as it has not been registerd.");
        }

        var descriptorIndex = this.Services.IndexOf<IDocumentStoreClient<T>>();
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
                         s.ServiceType.ImplementsInterface(typeof(IDocumentStoreClient<T>)))
                     ?.ToList())
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