// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

public class DocumentStoreCache : IDistributedCache
{
    private readonly IDocumentStoreClient<CacheDocument> client;

    public DocumentStoreCache(IDocumentStoreClient<CacheDocument> client)
    {
        EnsureArg.IsNotNull(client, nameof(client));

        this.client = client;
    }

    public byte[] Get(string key)
    {
        return this.GetAsync(key).Result;
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        EnsureArg.IsNotNullOrEmpty(key, nameof(key));
        token.ThrowIfCancellationRequested();

        var document = (await this.client.FindAsync(new DocumentKey("storage-cache", key), cancellationToken: token))?.FirstOrDefault();

        if (document != null)
        {
            if (!document.AbsoluteExpiration.HasValue)
            {
                return document.Value;
            }

            if (document.AbsoluteExpiration.HasValue && !document.SlidingExpiration.HasValue &&
                DateTimeOffset.UtcNow <= document.AbsoluteExpiration.Value)
            {
                return document.Value;
            }

            if (document.AbsoluteExpiration.HasValue && document.SlidingExpiration.HasValue && document.SlidingExpiration.HasValue &&
                DateTimeOffset.UtcNow <= document.AbsoluteExpiration.Value.AddMilliseconds(document.SlidingExpiration.Value.TotalMilliseconds))
            {
                await this.RefreshDocumentAsync(key, document, token);

                return document.Value;
            }

            await this.RemoveAsync(key, token);
        }

        return null;
    }

    public void Refresh(string key)
    {
        this.RefreshAsync(key).Wait();
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        EnsureArg.IsNotNullOrEmpty(key, nameof(key));
        token.ThrowIfCancellationRequested();

        var document = (await this.client.FindAsync(new DocumentKey("storage-cache", key), cancellationToken: token))?.FirstOrDefault();

        await this.RefreshDocumentAsync(key, document, token);
    }

    public void Remove(string key)
    {
        this.RemoveAsync(key).Wait();
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await this.client.DeleteAsync(new DocumentKey("storage-cache", key), cancellationToken: token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        this.SetAsync(key, value, options).Wait();
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
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

        if (document.AbsoluteExpiration.HasValue && document.SlidingExpiration.HasValue && document.SlidingExpiration.HasValue &&
            DateTimeOffset.UtcNow <= document.AbsoluteExpiration.Value.AddMilliseconds(document.SlidingExpiration.Value.TotalMilliseconds))
        {
            // refresh sliding expiration
            await this.SetAsync(key, document.Value, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.UtcNow.Add(document.SlidingExpiration.Value),
                SlidingExpiration = document.SlidingExpiration.Value
            }, token);
        }
    }
}

public class CacheDocument
{
    public byte[] Value { get; set; }

    public DateTimeOffset? AbsoluteExpiration { get; set; }

    public TimeSpan? SlidingExpiration { get; set; }
}