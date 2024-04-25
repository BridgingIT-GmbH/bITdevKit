// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

public class DocumentStoreCacheProvider : ICacheProvider
{
    private readonly ILogger<DocumentStoreCacheProvider> logger;
    private readonly IDistributedCache cache;
    private readonly IDocumentStoreClient<CacheDocument> client;
    private readonly ISerializer serializer;
    private readonly DocumentStoreCacheProviderConfiguration configuration;

    public DocumentStoreCacheProvider(
        ILoggerFactory loggerFactory,
        IDistributedCache cache,
        IDocumentStoreClient<CacheDocument> client,
        ISerializer serializer = null,
        DocumentStoreCacheProviderConfiguration configuration = null)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));
        EnsureArg.IsNotNull(cache, nameof(cache));
        EnsureArg.IsNotNull(client, nameof(client));

        this.logger = loggerFactory.CreateLogger<DocumentStoreCacheProvider>();
        this.cache = cache;
        this.client = client;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.configuration = configuration ?? new DocumentStoreCacheProviderConfiguration();
    }

    public T Get<T>(string key)
    {
        var value = this.cache.Get(key);

        return (value is not null) ? this.serializer.Deserialize<T>(value) : default;
    }

    public async Task<T> GetAsync<T>(string key, CancellationToken token = default)
    {
        var value = await this.cache.GetAsync(key, token);

        return (value is not null) ? this.serializer.Deserialize<T>(value) : default;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = this.Get<T>(key);

        return value is not null;
    }

    public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default)
    {
        return Task.FromResult(this.TryGet(key, out value)); // TryGetAsync cannot be used here due to out argument
    }

    public IEnumerable<string> GetKeys()
    {
        return this.GetKeysAsync().Result;
    }

    public async Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default)
    {
        var documents = await this.client.ListAsync(new DocumentKey("storage-cache", string.Empty), DocumentKeyFilter.RowKeyPrefixMatch, token);

        return documents.SafeNull().Select(d => d.RowKey);
    }

    public void Remove(string key)
    {
        this.cache.Remove(key);
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await this.cache.RemoveAsync(key, token);
    }

    public void RemoveStartsWith(string key)
    {
        this.RemoveStartsWithAsync(key).Wait();
    }

    public async Task RemoveStartsWithAsync(string key, CancellationToken token = default)
    {
        var documents = await this.client.ListAsync(new DocumentKey("storage-cache", key), DocumentKeyFilter.RowKeyPrefixMatch, token);

        foreach (var document in documents.SafeNull())
        {
            await this.client.DeleteAsync(new DocumentKey(document.PartitionKey, document.RowKey), token);
        }
    }

    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
    {
        this.cache.Set(
            key,
            this.serializer.SerializeToBytes(value),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration ?? this.configuration.AbsoluteExpiration,
                SlidingExpiration = slidingExpiration ?? this.configuration.SlidingExpiration
            });
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        await this.cache.SetAsync(
            key,
            this.serializer.SerializeToBytes(value),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration ?? this.configuration.AbsoluteExpiration,
                SlidingExpiration = slidingExpiration ?? this.configuration.SlidingExpiration
            },
            cancellationToken);
    }
}
