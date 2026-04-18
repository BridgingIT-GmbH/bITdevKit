// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Implements <see cref="IDistributedCache" /> by storing cache entries as documents.
/// </summary>
/// <remarks>
/// This adapter is used internally by <see cref="DocumentStoreCacheProvider" />, but it can also be used directly anywhere an
/// <see cref="IDistributedCache" /> is expected.
/// </remarks>
/// <example>
/// <code>
/// var distributedCache = new DocumentStoreCache(cacheDocuments);
/// await distributedCache.SetStringAsync("customer:42", jsonPayload, cancellationToken);
/// </code>
/// </example>
public class DocumentStoreCache : IDistributedCache
{
    private readonly IDocumentStoreClient<CacheDocument> client;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStoreCache" /> class.
    /// </summary>
    /// <param name="client">The document-store client that persists <see cref="CacheDocument" /> records.</param>
    public DocumentStoreCache(IDocumentStoreClient<CacheDocument> client)
    {
        EnsureArg.IsNotNull(client, nameof(client));

        this.client = client;
    }

    /// <inheritdoc />
    public byte[] Get(string key)
    {
        return this.GetAsync(key).Result;
    }

    /// <inheritdoc />
    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        EnsureArg.IsNotNullOrEmpty(key, nameof(key));
        token.ThrowIfCancellationRequested();

        var document = (await this.client.FindAsync(new DocumentKey("storage-cache", key), token))?.FirstOrDefault();

        if (document != null)
        {
            if (!document.AbsoluteExpiration.HasValue)
            {
                return document.Value;
            }

            if (document.AbsoluteExpiration.HasValue &&
                !document.SlidingExpiration.HasValue &&
                DateTimeOffset.UtcNow <= document.AbsoluteExpiration.Value)
            {
                return document.Value;
            }

            if (document.AbsoluteExpiration.HasValue &&
                document.SlidingExpiration.HasValue &&
                document.SlidingExpiration.HasValue &&
                DateTimeOffset.UtcNow <=
                document.AbsoluteExpiration.Value.AddMilliseconds(document.SlidingExpiration.Value.TotalMilliseconds))
            {
                await this.RefreshDocumentAsync(key, document, token);

                return document.Value;
            }

            await this.RemoveAsync(key, token);
        }

        return null;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        this.RefreshAsync(key).Wait();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        EnsureArg.IsNotNullOrEmpty(key, nameof(key));
        token.ThrowIfCancellationRequested();

        var document = (await this.client.FindAsync(new DocumentKey("storage-cache", key), token))?.FirstOrDefault();

        await this.RefreshDocumentAsync(key, document, token);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        this.RemoveAsync(key).Wait();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await this.client.DeleteAsync(new DocumentKey("storage-cache", key), token);
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        this.SetAsync(key, value, options).Wait();
    }

    /// <inheritdoc />
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        var document = new CacheDocument
        {
            Value = value,
            AbsoluteExpiration = options?.AbsoluteExpiration,
            SlidingExpiration = options?.SlidingExpiration
        };

        await this.client.UpsertAsync(new DocumentKey("storage-cache", key), document, token);
    }

    private async Task RefreshDocumentAsync(string key, CacheDocument document, CancellationToken token = default)
    {
        if (document is null)
        {
            return;
        }

        if (document.AbsoluteExpiration.HasValue &&
            document.SlidingExpiration.HasValue &&
            document.SlidingExpiration.HasValue &&
            DateTimeOffset.UtcNow <=
            document.AbsoluteExpiration.Value.AddMilliseconds(document.SlidingExpiration.Value.TotalMilliseconds))
        {
            // refresh sliding expiration
            await this.SetAsync(key,
                document.Value,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = DateTime.UtcNow.Add(document.SlidingExpiration.Value),
                    SlidingExpiration = document.SlidingExpiration.Value
                },
                token);
        }
    }
}

/// <summary>
/// Represents a cache entry persisted through the document-store-backed cache implementation.
/// </summary>
public class CacheDocument
{
    /// <summary>
    /// Gets or sets the raw cached value.
    /// </summary>
    public byte[] Value { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration timestamp of the cache entry.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the sliding-expiration window of the cache entry.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }
}
