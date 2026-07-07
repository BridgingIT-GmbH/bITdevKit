// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds exact-key read-through caching and write invalidation to an <see cref="IDocumentStoreClient{T}" />.
/// </summary>
/// <typeparam name="T">The document type handled by the decorated client.</typeparam>
public class CacheDocumentStoreClientBehavior<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    private readonly string type;
    private readonly ICacheProvider cacheProvideder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheDocumentStoreClientBehavior{T}" /> class.
    /// </summary>
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
    public async Task<Result<T>> GetResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        var cacheKey = this.CreateExactCacheKey(documentKey);
        if (await this.cacheProvideder.TryGetAsync(cacheKey, out T cachedEntity, cancellationToken))
        {
            return Result<T>.Success(cachedEntity);
        }

        var result = await this.Inner.GetResultAsync(documentKey, cancellationToken);
        if (result.IsSuccess)
        {
            await this.cacheProvideder.SetAsync(cacheKey,
                result.Value,
                this.Options.SlidingExpiration,
                this.Options.AbsoluteExpiration,
                cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Result<DocumentPage<T>>> FindPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.Inner.FindPageResultAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.Inner.ListPageResultAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) =>
        this.Inner.CountResultAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.Inner.ExistsResultAsync(documentKey, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        var result = await this.Inner.UpsertResultAsync(documentKey, entity, cancellationToken);
        if (result.IsSuccess)
        {
            await this.InvalidateAsync(documentKey, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        var materialized = entities.SafeNull().ToList();
        var result = await this.Inner.UpsertResultAsync(materialized, cancellationToken);
        if (result.IsSuccess)
        {
            foreach (var entity in materialized)
            {
                await this.InvalidateAsync(entity.DocumentKey, cancellationToken);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        var result = await this.Inner.DeleteResultAsync(documentKey, cancellationToken);
        if (result.IsSuccess)
        {
            await this.InvalidateAsync(documentKey, cancellationToken);
        }

        return result;
    }

    private string CreateExactCacheKey(DocumentKey documentKey) =>
        $"storage-{this.type}-get-{documentKey.PartitionKey}-{documentKey.RowKey}";

    private async Task InvalidateAsync(DocumentKey documentKey, CancellationToken cancellationToken)
    {
        await this.cacheProvideder.RemoveAsync(this.CreateExactCacheKey(documentKey), cancellationToken);
        await this.cacheProvideder.RemoveStartsWithAsync($"storage-{this.type}-", cancellationToken);
    }
}
