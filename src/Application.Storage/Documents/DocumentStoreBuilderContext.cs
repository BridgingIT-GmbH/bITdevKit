// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Configuration;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>Provides the direct single-client behavior builder used by provider-specific registration overloads.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>builder.WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;();</code></example>
public sealed class DocumentStoreBuilderContext<T>(IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped, IConfiguration configuration = null)
    where T : class, new()
{
    /// <summary>Gets the configured services.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>Gets the client lifetime.</summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>Gets optional configuration.</summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>Selects auditing once for the directly registered client, with explicit read selection.</summary>
    /// <example><code>builder.WithAuditing(new() { SelectRead = operation => operation == "get" });</code></example>
    public DocumentStoreBuilderContext<T> WithAuditing(StorageAuditOptions options = null)
    {
        var catalog = AuditBoundaryRegistrationCatalog.GetOrAdd(this.Services, "DocumentStorage", "Client");
        var alias = typeof(T).Name + ":direct";
        if (catalog.Registrations.Any(item => item.Boundary == "Client" && item.TypeAlias == alias && item.Selection == "selected")) return this;
        this.Services.Decorate<IDocumentStoreClient<T>>((inner, provider) => new AuditingDocumentStoreClientBehavior<T>(inner, provider.GetRequiredService<IServiceScopeFactory>(), options));
        catalog.Declare("Client", alias, "selected");
        return this;
    }

    /// <summary>Adds a decorator to the directly registered client.</summary>
    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>() where TBehavior : class, IDocumentStoreClient<T>
    {
        if (typeof(TBehavior) == typeof(AuditingDocumentStoreClientBehavior<T>)) return this.WithAuditing();
        this.Services.Decorate<IDocumentStoreClient<T>, TBehavior>();
        return this;
    }

    /// <summary>Adds a factory-created decorator to the directly registered client.</summary>
    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>(Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        ArgumentNullException.ThrowIfNull(behavior);
        this.Services.Decorate<IDocumentStoreClient<T>>((inner, _) => behavior(inner));
        return this;
    }

    /// <summary>Adds a service-provider-aware decorator to the directly registered client.</summary>
    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>(Func<IDocumentStoreClient<T>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        ArgumentNullException.ThrowIfNull(behavior);
        this.Services.Decorate<IDocumentStoreClient<T>>((inner, provider) => behavior(inner, provider));
        return this;
    }
}
