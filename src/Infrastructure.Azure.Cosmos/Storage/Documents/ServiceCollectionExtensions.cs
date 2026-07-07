// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Azure.Cosmos;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure;
using Configuration;
using Extensions;
using Logging;
using Scrutor;

/// <summary>
/// Provides Cosmos document-store service registration extensions.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a Cosmos DB backed document-store client for <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The Cosmos document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddCosmosDocumentStoreClient&lt;Person&gt;(
    ///     documentStoreOptions: new DocumentStoreOptions { MaxTake = 500 });
    /// </code>
    /// </example>
    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));

        return services.AddCosmosDocumentStoreClient<T>(o => o, lifetime, documentStoreOptions);
    }

    /// <summary>
    /// Registers a Cosmos DB backed document-store client for <typeparamref name="T" /> using an existing Cosmos client.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="cosmosClient">The Cosmos client used by the provider.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The Cosmos document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddCosmosDocumentStoreClient&lt;Person&gt;(
    ///     cosmosClient,
    ///     documentStoreOptions: new DocumentStoreOptions { DefaultTake = 100 });
    /// </code>
    /// </example>
    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        CosmosClient cosmosClient,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
        where T : class, new()
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(cosmosClient, nameof(cosmosClient));

        return services.AddCosmosDocumentStoreClient<T>(o => o.Client(cosmosClient), lifetime, documentStoreOptions);
    }

    /// <summary>
    /// Registers a Cosmos DB backed document-store client for <typeparamref name="T" /> using provider options.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="providerOptionsBuilder">The provider options builder used to configure the Cosmos SQL provider.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The Cosmos document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddCosmosDocumentStoreClient&lt;Person&gt;(
    ///     options => options.Container("storage_documents").PartitionKey(e => e.Type),
    ///     documentStoreOptions: new DocumentStoreOptions { MaxTake = 500 });
    /// </code>
    /// </example>
    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        Builder<CosmosSqlProviderOptionsBuilder<CosmosStorageDocument>, CosmosSqlProviderOptions<CosmosStorageDocument>>
            providerOptionsBuilder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
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
                        new CosmosDocumentStoreProvider(
                            new CosmosSqlProvider<CosmosStorageDocument>(providerOptions),
                            options: documentStoreOptions));
                });

                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<IDocumentStoreClient<T>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();

                    return new DocumentStoreClient<T>(
                        new CosmosDocumentStoreProvider(
                            new CosmosSqlProvider<CosmosStorageDocument>(providerOptions),
                            options: documentStoreOptions));
                });

                break;
            default:
                services.TryAddScoped<IDocumentStoreClient<T>>(sp =>
                {
                    providerOptions.Client ??= sp.GetService<CosmosClient>();
                    providerOptions.LoggerFactory ??= sp.GetRequiredService<ILoggerFactory>();

                    return new DocumentStoreClient<T>(
                        new CosmosDocumentStoreProvider(
                            new CosmosSqlProvider<CosmosStorageDocument>(providerOptions),
                            options: documentStoreOptions));
                });

                break;
        }

        return new CosmosDocumentStoreClientBuilderContext<T>(services, lifetime);
    }

    /// <summary>
    /// Registers a Cosmos DB backed document-store client for <typeparamref name="T" /> using a pre-built provider.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="services">The service collection to update.</param>
    /// <param name="provider">The pre-built Cosmos document-store provider.</param>
    /// <param name="lifetime">The client lifetime to register.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The Cosmos document-store builder for adding behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddCosmosDocumentStoreClient&lt;Person&gt;(provider);
    /// </code>
    /// </example>
    public static CosmosDocumentStoreClientBuilderContext<T> AddCosmosDocumentStoreClient<T>(
        this IServiceCollection services,
        CosmosDocumentStoreProvider provider,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        DocumentStoreOptions documentStoreOptions = null)
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

/// <summary>
/// Provides fluent configuration for a Cosmos document-store client registration.
/// </summary>
/// <typeparam name="T">The document payload type.</typeparam>
/// <param name="services">The service collection containing the document-store registration.</param>
/// <param name="lifetime">The registered document-store client lifetime.</param>
/// <param name="configuration">The optional configuration associated with the registration.</param>
/// <example>
/// <code>
/// services.AddCosmosDocumentStoreClient&lt;Person&gt;()
///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;();
/// </code>
/// </example>
public class CosmosDocumentStoreClientBuilderContext<T>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null)
    where T : class, new()
{
    private readonly List<Action<IServiceCollection>> behaviors = [];
    private ServiceDescriptor repositoryDescriptor;

    /// <summary>
    /// Gets the service collection containing the document-store registration.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the registered document-store client lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the optional configuration associated with the registration.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Adds a document-store client behavior using dependency injection.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type that decorates <see cref="IDocumentStoreClient{T}" />.</typeparam>
    /// <returns>The current builder context.</returns>
    public CosmosDocumentStoreClientBuilderContext<T> WithBehavior<TBehavior>()
        where TBehavior : class, IDocumentStoreClient<T>
    {
        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Adds a document-store client behavior using a factory.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type that decorates <see cref="IDocumentStoreClient{T}" />.</typeparam>
    /// <param name="behavior">The behavior factory that receives the inner document-store client.</param>
    /// <returns>The current builder context.</returns>
    public CosmosDocumentStoreClientBuilderContext<T> WithBehavior<TBehavior>(
        Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((service, _) => behavior(service)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Adds a document-store client behavior using a factory with service-provider access.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type that decorates <see cref="IDocumentStoreClient{T}" />.</typeparam>
    /// <param name="behavior">The behavior factory that receives the inner document-store client and service provider.</param>
    /// <returns>The current builder context.</returns>
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
