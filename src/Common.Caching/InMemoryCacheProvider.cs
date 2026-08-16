// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
///     Stores cache entries in an <see cref="IMemoryCache"/> and logs cache access at debug level.
/// </summary>
public partial class InMemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache cache;
    private readonly InMemoryCacheProviderConfiguration configuration;
    private readonly ILogger<InMemoryCacheProvider> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InMemoryCacheProvider"/> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create the provider logger.</param>
    /// <param name="cache">The in-memory cache that stores entries.</param>
    /// <param name="configuration">Default expiration settings, or <see langword="null"/> to use no defaults.</param>
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

    /// <inheritdoc/>
    public T Get<T>(string key)
    {
        return this.TryGet(key, out T value) ? value : default;
    }

    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(string key, CancellationToken token = default)
    {
        return await this.TryGetAsync(key, out T value, token) ? value : default;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public IEnumerable<string> GetKeys()
    {
        return this.cache.GetKeys<string>();
    }

    /// <inheritdoc/>
    public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default)
    {
        return Task.FromResult(this.cache.GetKeys<string>());
    }

    /// <inheritdoc/>
    public void Remove(string key)
    {
        TypedLogger.LogCacheRemove(this.logger, key);
        this.cache.Remove(key);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        this.Remove(key);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void RemoveStartsWith(string key)
    {
        TypedLogger.LogCacheRemove(this.logger, $"{key}*");
        this.cache.RemoveStartsWith(key);
    }

    /// <inheritdoc/>
    public Task RemoveStartsWithAsync(string key, CancellationToken token = default)
    {
        this.RemoveStartsWith(key);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <summary>
    ///     Provides source-generated messages for in-memory cache activity.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        ///     Logs a cache hit.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="cacheKey">The matching cache key.</param>
        [LoggerMessage(0, LogLevel.Debug, "cache hit (key={CacheKey})")]
        public static partial void LogCacheHit(ILogger logger, string cacheKey);

        /// <summary>
        ///     Logs a cache miss.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="cacheKey">The requested cache key.</param>
        [LoggerMessage(1, LogLevel.Debug, "cache miss (key={CacheKey})")]
        public static partial void LogCacheMiss(ILogger logger, string cacheKey);

        /// <summary>
        ///     Logs a cache write.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="cacheKey">The written cache key.</param>
        [LoggerMessage(2, LogLevel.Debug, "cache set (key={CacheKey})")]
        public static partial void LogCacheSet(ILogger logger, string cacheKey);

        /// <summary>
        ///     Logs a cache removal.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="cacheKey">The removed cache key or key pattern.</param>
        [LoggerMessage(3, LogLevel.Debug, "cache remove (key={CacheKey})")]
        public static partial void LogCacheRemove(ILogger logger, string cacheKey);
    }
}
