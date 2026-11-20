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
        if (await this.cache.TryGetAsync(this.Key(key), out CacheEntry cached, cancellationToken))
        {
            this.logger.LogDebug("{LogKey} document cache hit (type={DocumentType})", Constants.LogKey, typeof(T).Name);
            return Result<DocumentEntry<T>>.Success(FromCache(cached));
        }

        var result = await base.GetAsync(key, cancellationToken);
        if (result.IsSuccess)
        {
            await this.cache.SetAsync(this.Key(key), ToCache(result.Value), this.options.SlidingExpiration, this.options.AbsoluteExpiration, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<IReadOnlyList<Result<DocumentEntry<T>>>>> GetManyAsync(IReadOnlyList<DocumentKey> keys, CancellationToken cancellationToken = default)
    {
        if (keys is null || keys.Count == 0)
        {
            return await base.GetManyAsync(keys, cancellationToken);
        }

        var results = new Result<DocumentEntry<T>>[keys.Count];
        var hits = new bool[keys.Count];
        var misses = new List<DocumentKey>();
        var missed = new HashSet<DocumentKey>();
        for (var index = 0; index < keys.Count; index++)
        {
            if (await this.cache.TryGetAsync(this.Key(keys[index]), out CacheEntry cached, cancellationToken))
            {
                results[index] = Result<DocumentEntry<T>>.Success(FromCache(cached));
                hits[index] = true;
            }
            else if (missed.Add(keys[index]))
            {
                misses.Add(keys[index]);
            }
        }

        if (misses.Count == 0)
        {
            return Result<IReadOnlyList<Result<DocumentEntry<T>>>>.Success(results);
        }

        var fetched = await base.GetManyAsync(misses, cancellationToken);
        if (fetched.IsFailure)
        {
            return fetched;
        }

        var byKey = new Dictionary<DocumentKey, Result<DocumentEntry<T>>>(misses.Count);
        for (var index = 0; index < misses.Count; index++)
        {
            byKey[misses[index]] = fetched.Value[index];
            if (fetched.Value[index].IsSuccess)
            {
                await this.cache.SetAsync(this.Key(misses[index]), ToCache(fetched.Value[index].Value), this.options.SlidingExpiration, this.options.AbsoluteExpiration, cancellationToken);
            }
        }

        for (var index = 0; index < results.Length; index++)
        {
            if (!hits[index])
            {
                var result = byKey[keys[index]];
                results[index] = result.IsSuccess
                    ? Result<DocumentEntry<T>>.Success(result.Value with { Properties = DocumentPropertyValues.Clone(result.Value.Properties) })
                    : result;
            }
        }

        return Result<IReadOnlyList<Result<DocumentEntry<T>>>>.Success(results);
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertAsync(key, value, options, cancellationToken);
        if (result.IsSuccess || result.HasError<DocumentStoreIndeterminateOperationError>()) await this.cache.RemoveAsync(this.Key(key), cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertManyAsync(writes, cancellationToken);
        if (result.HasError<DocumentStoreIndeterminateOperationError>())
        {
            foreach (var write in writes) await this.cache.RemoveAsync(this.Key(write.Key), cancellationToken);
        }
        else if (result.IsSuccess)
        {
            foreach (var item in result.Value.Items) await this.cache.RemoveAsync(this.Key(item.Key), cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpdateDimensionsAsync(DocumentDimensionsUpdate update, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdateDimensionsAsync(update, cancellationToken);
        if (result.IsSuccess || result.HasError<DocumentStoreIndeterminateOperationError>()) await this.cache.RemoveAsync(this.Key(update.Key), cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default)
    {
        var result = await base.UpdatePropertiesAsync(update, cancellationToken);
        if (result.IsSuccess || result.HasError<DocumentStoreIndeterminateOperationError>()) await this.cache.RemoveAsync(this.Key(update.Key), cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteAsync(key, options, cancellationToken);
        if (result.IsSuccess || result.HasError<DocumentStoreIndeterminateOperationError>()) await this.cache.RemoveAsync(this.Key(key), cancellationToken);
        return result;
    }

    private static CacheEntry ToCache(DocumentEntry<T> value) => new()
    {
        Entry = value with { Properties = new() },
        PropertiesJson = DocumentPropertyValues.Serialize(value.Properties)
    };

    private static DocumentEntry<T> FromCache(CacheEntry value) => value.Entry with
    {
        Properties = DocumentPropertyValues.Deserialize(value.PropertiesJson)
    };

    private sealed record CacheEntry
    {
        public DocumentEntry<T> Entry { get; init; }

        public string PropertiesJson { get; init; }
    }

    private string Key(DocumentKey key)
    {
        var identity = $"{typeof(T).AssemblyQualifiedName}\0{key.PartitionKey}\0{key.RowKey}";
        return $"bdk_document_v2_{((IDocumentStoreClientIdentity)this).ClientName}_{ContentHashHelper.ComputeSha256(identity)}";
    }
}
