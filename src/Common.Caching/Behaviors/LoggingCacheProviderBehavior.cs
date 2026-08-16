// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Behaviors;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Defines a logging cache-provider decorator whose cache operations are not currently implemented.
/// </summary>
public partial class LoggingCacheProviderBehavior : ICacheProvider
{
    private readonly ICacheProvider inner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LoggingCacheProviderBehavior"/> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create the decorator logger, or <see langword="null"/> to use a null logger.</param>
    /// <param name="inner">The cache provider intended to be decorated.</param>
    public LoggingCacheProviderBehavior(ILoggerFactory loggerFactory, ICacheProvider inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<LoggingCacheProviderBehavior>() ??
            NullLoggerFactory.Instance.CreateLogger<LoggingCacheProviderBehavior>();
        this.inner = inner;
    }

    /// <summary>
    ///     Gets the logger used by the decorator.
    /// </summary>
    protected ILogger<LoggingCacheProviderBehavior> Logger { get; }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public T Get<T>(string key)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public Task<T> GetAsync<T>(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public IEnumerable<string> GetKeys()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public void Remove(string key)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public void RemoveStartsWith(string key)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public Task RemoveStartsWithAsync(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public void Set<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public bool TryGet<T>(string key, out T value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException">The operation is not implemented.</exception>
    public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Provides source-generated cache logging messages.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        ///     Logs that a value was added to the in-process cache.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="cacheKey">The cache key that was added.</param>
        [LoggerMessage(0, LogLevel.Information, "inprocess cache add (key={CacheKey})")]
        public static partial void LogCacheAdd(ILogger logger, string cacheKey);

        /// <summary>
        ///     Logs that a value was removed from the in-process cache.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="cacheKey">The cache key that was removed.</param>
        [LoggerMessage(1, LogLevel.Information, "inprocess cache 'remove' (key={CacheKey})")]
        public static partial void LogCacheRemove(ILogger logger, string cacheKey);
    }
}
