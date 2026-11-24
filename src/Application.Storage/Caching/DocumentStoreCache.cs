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
    private readonly TimeProvider clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStoreCache" /> class.
    /// </summary>
    /// <param name="client">The document-store client that persists <see cref="CacheDocument" /> records.</param>
    public DocumentStoreCache(IDocumentStoreClient<CacheDocument> client) : this(client, TimeProvider.System)
    {
    }

    /// <summary>Creates the document-cache facade with an explicit expiration clock.</summary>
    /// <param name="client">The document client storing cache payloads and deadlines.</param>
    /// <param name="timeProvider">The clock shared with the document client.</param>
    public DocumentStoreCache(IDocumentStoreClient<CacheDocument> client, TimeProvider timeProvider)
    {
        EnsureArg.IsNotNull(client, nameof(client));

        this.client = client;
        this.clock = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public byte[] Get(string key)
    {
        return this.GetAsync(key).Result;
    }

    /// <inheritdoc />
    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        var entry = await this.ReadEntryAsync(key, token).ConfigureAwait(false);
        if (entry is null) return null;
        await this.RefreshEntryAsync(entry, token).ConfigureAwait(false);
        return entry.Value.Value;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        this.RefreshAsync(key).Wait();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        var entry = await this.ReadEntryAsync(key, token).ConfigureAwait(false);
        if (entry is not null) await this.RefreshEntryAsync(entry, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        this.RemoveAsync(key).Wait();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await this.client.DeleteAsync(new DocumentKey("storage-cache", key), cancellationToken: token);
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
        EnsureArg.IsNotNullOrEmpty(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        token.ThrowIfCancellationRequested();
        var now = this.clock.GetUtcNow();
        var absolute = options.AbsoluteExpiration;
        if (options.AbsoluteExpirationRelativeToNow is TimeSpan relative)
        {
            var relativeAt = now + relative;
            absolute = absolute.HasValue && absolute.Value < relativeAt ? absolute : relativeAt;
        }

        var due = absolute;
        if (options.SlidingExpiration is TimeSpan sliding)
        {
            var slidingAt = now + sliding;
            due = due.HasValue && due.Value < slidingAt ? due : slidingAt;
        }

        var result = await this.client.UpsertAsync(new("storage-cache", key), new CacheDocument
        {
            Value = value.ToArray(), AbsoluteExpiration = absolute, SlidingExpiration = options.SlidingExpiration
        }, new DocumentWriteOptions { Expiration = due.HasValue ? ExpirationChange.At(due.Value) : ExpirationChange.Clear }, token).ConfigureAwait(false);
        if (result.IsFailure) throw new IOException("The document cache write failed.");
    }

    private async Task<DocumentEntry<CacheDocument>> ReadEntryAsync(string key, CancellationToken token)
    {
        EnsureArg.IsNotNullOrEmpty(key, nameof(key));
        token.ThrowIfCancellationRequested();
        var result = await this.client.GetAsync(new("storage-cache", key), token).ConfigureAwait(false);
        if (result.IsFailure)
        {
            if (result.Errors.OfType<DocumentStoreNotFoundError>().Any()) return null;
            throw new IOException("The document cache read failed.");
        }

        return result.Value.Value.AbsoluteExpiration <= this.clock.GetUtcNow() ? null : result.Value;
    }

    private async Task RefreshEntryAsync(DocumentEntry<CacheDocument> entry, CancellationToken token)
    {
        if (entry.Value.SlidingExpiration is not TimeSpan sliding) return;
        var due = this.clock.GetUtcNow() + sliding;
        if (entry.Value.AbsoluteExpiration is DateTimeOffset cap && cap < due) due = cap;
        var result = await this.client.UpdatePropertiesAsync(new(entry.Key)
        {
            IfMatchETag = entry.ETag, Expiration = ExpirationChange.At(due)
        }, token).ConfigureAwait(false);
        if (result.IsFailure && !result.Errors.Any(error => error is DocumentStoreConflictError or DocumentStoreNotFoundError)) throw new IOException("The document cache refresh failed.");
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
