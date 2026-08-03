// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Adds exact-key read-through caching and mutation invalidation.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new CacheDocumentStoreClientBehavior&lt;Person&gt;(loggerFactory, inner, cache);</code></example>
public class CacheDocumentStoreClientBehavior<T>(ILoggerFactory loggerFactory, IDocumentStoreClient<T> inner, ICacheProvider cacheProvider, CacheDocumentStoreClientBehaviorOptions options = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    private readonly ILogger<CacheDocumentStoreClientBehavior<T>> logger = loggerFactory?.CreateLogger<CacheDocumentStoreClientBehavior<T>>() ?? NullLogger<CacheDocumentStoreClientBehavior<T>>.Instance;
    private readonly ICacheProvider cache = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
    private readonly CacheDocumentStoreClientBehaviorOptions options = options ?? new();

    /// <inheritdoc />
    public override async Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        if (await this.cache.TryGetAsync(this.Key(key), out DocumentEntry<T> cached, cancellationToken))
        {
            this.logger.LogDebug("{LogKey} document cache hit (type={DocumentType})", Constants.LogKey, typeof(T).Name);
            return Result<DocumentEntry<T>>.Success(cached);
        }

        var result = await base.GetAsync(key, cancellationToken);
        if (result.IsSuccess)
        {
            await this.cache.SetAsync(this.Key(key), result.Value, this.options.SlidingExpiration, this.options.AbsoluteExpiration, cancellationToken);
        }
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertAsync(key, value, options, cancellationToken);
        if (result.IsSuccess) await this.cache.RemoveAsync(this.Key(key), cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdatePropertiesAsync(update, cancellationToken);
        if (result.IsSuccess) await this.cache.RemoveAsync(this.Key(update.Key), cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteAsync(key, options, cancellationToken);
        if (result.IsSuccess) await this.cache.RemoveAsync(this.Key(key), cancellationToken);
        return result;
    }

    private string Key(DocumentKey key)
    {
        var identity = $"{typeof(T).AssemblyQualifiedName}\0{key.PartitionKey}\0{key.RowKey}";
        return $"bdk_document_{((IDocumentStoreClientIdentity)this).ClientName}_{ContentHashHelper.ComputeSha256(identity)}";
    }
}
