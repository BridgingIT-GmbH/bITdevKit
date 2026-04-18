// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds read-through caching and write invalidation to an <see cref="IDocumentStoreClient{T}" />.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
/// <example>
/// <code>
/// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;()
///     .WithBehavior((inner, sp) => new CacheDocumentStoreClientBehavior&lt;Person&gt;(
///         sp.GetRequiredService&lt;ILoggerFactory&gt;(),
///         inner,
///         sp.GetRequiredService&lt;ICacheProvider&gt;(),
///         new CacheDocumentStoreClientBehaviorOptions
///         {
///             SlidingExpiration = TimeSpan.FromMinutes(5)
///         }));
/// </code>
/// </example>
public class CacheDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    private readonly string type;
    private readonly ICacheProvider cacheProvideder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the behavior logger.</param>
    /// <param name="inner">The inner client to decorate.</param>
    /// <param name="cacheProvideder">The cache provider used to store query results and invalidate them on writes.</param>
    /// <param name="options">The cache behavior options.</param>
    public CacheDocumentStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IDocumentStoreClient<T> inner,
        ICacheProvider cacheProvideder,
        CacheDocumentStoreClientBehaviorOptions options = null)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));
        EnsureArg.IsNotNull(cacheProvideder, nameof(cacheProvideder));

        this.Logger = loggerFactory?.CreateLogger<CacheDocumentStoreClientBehavior<T>>() ??
            NullLoggerFactory.Instance.CreateLogger<CacheDocumentStoreClientBehavior<T>>();
        this.Inner = inner;
        this.cacheProvideder = cacheProvideder;
        this.Options = options ?? new CacheDocumentStoreClientBehaviorOptions();
        this.type = typeof(T).Name;
    }

    /// <summary>
    /// Gets the logger used by the behavior.
    /// </summary>
    protected ILogger<CacheDocumentStoreClientBehavior<T>> Logger { get; }

    /// <summary>
    /// Gets the cache settings used by the behavior.
    /// </summary>
    protected CacheDocumentStoreClientBehaviorOptions Options { get; }

    /// <summary>
    /// Gets the decorated inner client.
    /// </summary>
    protected IDocumentStoreClient<T> Inner { get; }

    /// <inheritdoc />
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.Inner.DeleteAsync(documentKey, cancellationToken);
        await this.cacheProvideder.RemoveAsync(
            $"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken)
    {
        if (await this.cacheProvideder.TryGetAsync($"storage-{this.type}-find-all",
                out IEnumerable<T> cachedEntities,
                cancellationToken))
        {
            return cachedEntities;
        }

        var entities = await this.Inner.FindAsync(cancellationToken);
        await this.cacheProvideder.SetAsync($"storage-{this.type}-find-all",
            cachedEntities,
            this.Options.SlidingExpiration,
            this.Options.AbsoluteExpiration,
            cancellationToken);

        return entities;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        if (await this.cacheProvideder.TryGetAsync(
                $"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}",
                out IEnumerable<T> cachedEntities,
                cancellationToken))
        {
            return cachedEntities;
        }

        var entities = await this.Inner.FindAsync(documentKey, cancellationToken);
        await this.cacheProvideder.SetAsync($"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}",
            cachedEntities,
            this.Options.SlidingExpiration,
            this.Options.AbsoluteExpiration,
            cancellationToken);

        return entities;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (await this.cacheProvideder.TryGetAsync(
                $"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}-{filter}",
                out IEnumerable<T> cachedEntities,
                cancellationToken))
        {
            return cachedEntities;
        }

        var entities = await this.Inner.FindAsync(documentKey, filter, cancellationToken);
        await this.cacheProvideder.SetAsync(
            $"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}-{filter}",
            cachedEntities,
            this.Options.SlidingExpiration,
            this.Options.AbsoluteExpiration,
            cancellationToken);

        return entities;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken)
    {
        if (await this.cacheProvideder.TryGetAsync($"storage-{this.type}-list-all",
                out IEnumerable<DocumentKey> cachedKeys,
                cancellationToken))
        {
            return cachedKeys;
        }

        var keys = await this.Inner.ListAsync(cancellationToken);
        await this.cacheProvideder.SetAsync($"storage-{this.type}-list-all",
            keys,
            this.Options.SlidingExpiration,
            this.Options.AbsoluteExpiration,
            cancellationToken);

        return keys;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        if (await this.cacheProvideder.TryGetAsync(
                $"storage-{this.type}-list-{documentKey.PartitionKey}-{documentKey.RowKey}",
                out IEnumerable<DocumentKey> cachedKeys,
                cancellationToken))
        {
            return cachedKeys;
        }

        var keys = await this.Inner.ListAsync(documentKey, cancellationToken);
        await this.cacheProvideder.SetAsync($"storage-{this.type}-list-{documentKey.PartitionKey}-{documentKey.RowKey}",
            keys,
            this.Options.SlidingExpiration,
            this.Options.AbsoluteExpiration,
            cancellationToken);

        return keys;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (await this.cacheProvideder.TryGetAsync(
                $"storage-{this.type}-list-{documentKey.PartitionKey}-{documentKey.RowKey}-{filter}",
                out IEnumerable<DocumentKey> cachedKeys,
                cancellationToken))
        {
            return cachedKeys;
        }

        var keys = await this.Inner.ListAsync(cancellationToken);
        await this.cacheProvideder.SetAsync(
            $"storage-{this.type}-list-{documentKey.PartitionKey}-{documentKey.RowKey}-{filter}",
            keys,
            this.Options.SlidingExpiration,
            this.Options.AbsoluteExpiration,
            cancellationToken);

        return keys;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(cancellationToken); // currently not cached
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(documentKey, cancellationToken); // currently not cached
    }

    /// <inheritdoc />
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken)
    {
        await this.Inner.UpsertAsync(documentKey, entity, cancellationToken);

        await this.cacheProvideder.RemoveAsync(
            $"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}",
            cancellationToken);
        await this.cacheProvideder.RemoveStartsWithAsync(
            $"storage-{this.type}-find-{documentKey.PartitionKey}-{documentKey.RowKey}-",
            cancellationToken); // filtered
        await this.cacheProvideder.RemoveAsync($"storage-{this.type}-find-all", cancellationToken);
        await this.cacheProvideder.RemoveAsync(
            $"storage-{this.type}-list-{documentKey.PartitionKey}-{documentKey.RowKey}",
            cancellationToken);
        await this.cacheProvideder.RemoveStartsWithAsync(
            $"storage-{this.type}-list-{documentKey.PartitionKey}-{documentKey.RowKey}-",
            cancellationToken); // filtered
        await this.cacheProvideder.RemoveAsync($"storage-{this.type}-list-all", cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        await this.Inner.UpsertAsync(entities, cancellationToken);

        foreach (var entity in entities)
        {
            await this.cacheProvideder.RemoveAsync(
                $"storage-{this.type}-find-{entity.DocumentKey.PartitionKey}-{entity.DocumentKey.RowKey}",
                cancellationToken);
            await this.cacheProvideder.RemoveStartsWithAsync(
                $"storage-{this.type}-find-{entity.DocumentKey.PartitionKey}-{entity.DocumentKey.RowKey}-",
                cancellationToken); // filtered
            await this.cacheProvideder.RemoveAsync(
                $"storage-{this.type}-list-{entity.DocumentKey.PartitionKey}-{entity.DocumentKey.RowKey}",
                cancellationToken);
            await this.cacheProvideder.RemoveStartsWithAsync(
                $"storage-{this.type}-list-{entity.DocumentKey.PartitionKey}-{entity.DocumentKey.RowKey}-",
                cancellationToken); // filtered
        }

        await this.cacheProvideder.RemoveAsync($"storage-{this.type}-find-all", cancellationToken);
        await this.cacheProvideder.RemoveAsync($"storage-{this.type}-list-all", cancellationToken);
    }
}
