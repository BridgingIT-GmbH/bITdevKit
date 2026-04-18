// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.Caching.Distributed;

/// <summary>
/// Implements <see cref="ICacheProvider" /> on top of a distributed cache facade backed by document storage.
/// </summary>
/// <remarks>
/// This provider stores cache entries as <see cref="CacheDocument" /> records in the <c>storage-cache</c> partition of a
/// document store. It is useful when cache entries should survive process restarts or be shared across hosts.
/// </remarks>
/// <example>
/// <code>
/// builder.Services
///     .AddCaching(builder.Configuration)
///     .WithEntityFrameworkDocumentStoreProvider&lt;AppDbContext&gt;();
/// </code>
/// </example>
public class DocumentStoreCacheProvider : ICacheProvider
{
    private readonly ILogger<DocumentStoreCacheProvider> logger;
    private readonly IDistributedCache cache;
    private readonly IDocumentStoreClient<CacheDocument> client;
    private readonly ISerializer serializer;
    private readonly DocumentStoreCacheProviderConfiguration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStoreCacheProvider" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used to create the provider logger.</param>
    /// <param name="cache">The distributed-cache facade used for raw byte operations.</param>
    /// <param name="client">The document-store client used for key enumeration and prefix invalidation.</param>
    /// <param name="serializer">The serializer used to convert values to and from cached bytes.</param>
    /// <param name="configuration">The cache-provider configuration values.</param>
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

    /// <inheritdoc />
    public T Get<T>(string key)
    {
        var value = this.cache.Get(key);

        return value is not null ? this.serializer.Deserialize<T>(value) : default;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken token = default)
    {
        var value = await this.cache.GetAsync(key, token);

        return value is not null ? this.serializer.Deserialize<T>(value) : default;
    }

    /// <inheritdoc />
    public bool TryGet<T>(string key, out T value)
    {
        value = this.Get<T>(key);

        return value is not null;
    }

    /// <inheritdoc />
    public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default)
    {
        return Task.FromResult(this.TryGet(key, out value)); // TryGetAsync cannot be used here due to out argument
    }

    /// <inheritdoc />
    public IEnumerable<string> GetKeys()
    {
        return this.GetKeysAsync().Result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default)
    {
        var documents = await this.client.ListAsync(new DocumentKey("storage-cache", string.Empty),
            DocumentKeyFilter.RowKeyPrefixMatch,
            token);

        return documents.SafeNull().Select(d => d.RowKey);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        this.cache.Remove(key);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await this.cache.RemoveAsync(key, token);
    }

    /// <inheritdoc />
    public void RemoveStartsWith(string key)
    {
        this.RemoveStartsWithAsync(key).Wait();
    }

    /// <inheritdoc />
    public async Task RemoveStartsWithAsync(string key, CancellationToken token = default)
    {
        var documents = await this.client.ListAsync(new DocumentKey("storage-cache", key),
            DocumentKeyFilter.RowKeyPrefixMatch,
            token);

        foreach (var document in documents.SafeNull())
        {
            await this.client.DeleteAsync(new DocumentKey(document.PartitionKey, document.RowKey), token);
        }
    }

    /// <inheritdoc />
    public void Set<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null)
    {
        this.cache.Set(key,
            this.serializer.SerializeToBytes(value),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration ?? this.configuration.AbsoluteExpiration,
                SlidingExpiration = slidingExpiration ?? this.configuration.SlidingExpiration
            });
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        await this.cache.SetAsync(key,
            this.serializer.SerializeToBytes(value),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration ?? this.configuration.AbsoluteExpiration,
                SlidingExpiration = slidingExpiration ?? this.configuration.SlidingExpiration
            },
            cancellationToken);
    }
}
