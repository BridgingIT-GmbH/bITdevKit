// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

public partial class InMemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache cache;
    private readonly InMemoryCacheProviderConfiguration configuration;
    private readonly ILogger<InMemoryCacheProvider> logger;

    public InMemoryCacheProvider(
        ILoggerFactory loggerFactory,
        IMemoryCache cache,
        InMemoryCacheProviderConfiguration configuration = null)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));
        EnsureArg.IsNotNull(cache, nameof(cache));

        this.logger = loggerFactory.CreateLogger<InMemoryCacheProvider>();
        this.cache = cache;
        this.configuration = configuration ?? new InMemoryCacheProviderConfiguration();
    }

    public T Get<T>(string key)
    {
        return this.TryGet(key, out T value) ? value : default;
    }

    public async Task<T> GetAsync<T>(string key, CancellationToken token = default)
    {
        return await this.TryGetAsync(key, out T value, token) ? value : default;
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (this.cache.TryGetValue(key, out value))
        {
            TypedLogger.LogCacheHit(this.logger, key);

            return true;
        }

        TypedLogger.LogCacheMiss(this.logger, key);
        return false;
    }

    public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default)
    {
        if (this.cache.TryGetValue(key, out value))
        {
            TypedLogger.LogCacheHit(this.logger, key);

            return Task.FromResult(true);
        }

        TypedLogger.LogCacheMiss(this.logger, key);
        return Task.FromResult(false);
    }

    public IEnumerable<string> GetKeys()
    {
        return this.cache.GetKeys<string>();
    }

    public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default)
    {
        return Task.FromResult(this.cache.GetKeys<string>());
    }

    public void Remove(string key)
    {
        TypedLogger.LogCacheRemove(this.logger, key);
        this.cache.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        this.Remove(key);
        return Task.CompletedTask;
    }

    public void RemoveStartsWith(string key)
    {
        TypedLogger.LogCacheRemove(this.logger, $"{key}*");
        this.cache.RemoveStartsWith(key);
    }

    public Task RemoveStartsWithAsync(string key, CancellationToken token = default)
    {
        this.RemoveStartsWith(key);
        return Task.CompletedTask;
    }

    public void Set<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null)
    {
        TypedLogger.LogCacheSet(this.logger, key);

        // If the entry does not exist, it is created. If the specified entry exists, it is updated.
        this.cache.Set(key,
            value,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = slidingExpiration ?? this.configuration.SlidingExpiration,
                AbsoluteExpiration = absoluteExpiration ?? this.configuration.AbsoluteExpiration
            });
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken token = default)
    {
        this.Set(key, value, slidingExpiration, absoluteExpiration);
        return Task.CompletedTask;
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "cache hit (key={CacheKey})")]
        public static partial void LogCacheHit(ILogger logger, string cacheKey);

        [LoggerMessage(1, LogLevel.Debug, "cache miss (key={CacheKey})")]
        public static partial void LogCacheMiss(ILogger logger, string cacheKey);

        [LoggerMessage(2, LogLevel.Debug, "cache set (key={CacheKey})")]
        public static partial void LogCacheSet(ILogger logger, string cacheKey);

        [LoggerMessage(3, LogLevel.Debug, "cache remove (key={CacheKey})")]
        public static partial void LogCacheRemove(ILogger logger, string cacheKey);
    }
}