// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Provides cache query behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
/// <param name="provider">The provider used by the operation.</param>
public partial class CacheQueryBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, ICacheProvider provider)
    : QueryBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, MediatR.IRequest<TResponse>
{
    private readonly ICacheProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request)
    {
        return request is ICacheQuery;
    }

    /// <inheritdoc/>
    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // cache only if implements interface
        if (request is not ICacheQuery instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.Key.IsNullOrEmpty())
        {
            return await next().AnyContext();
        }

        var cacheKey = instance.Options.Key;
        if (this.provider.TryGet(cacheKey, out TResponse cachedResult))
        {
            TypedLogger.LogCacheHit(this.Logger, Constants.LogKey, cacheKey, this.GetType().Name);

            return cachedResult;
        }

        TypedLogger.LogCacheMiss(this.Logger, Constants.LogKey, cacheKey, this.GetType().Name);

        var result = await next().AnyContext(); // continue if not found in cache
        if (result is null)
        {
            return default;
        }

        TypedLogger.LogCacheAdd(this.Logger, Constants.LogKey, cacheKey, this.GetType().Name);
        this.provider.Set(cacheKey, result, instance.Options.SlidingExpiration, instance.Options.AbsoluteExpiration);

        return result;
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the cache hit operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="cacheKey">The cache key used by the operation.</param>
        /// <param name="behaviorType">The behavior type used by the operation.</param>
        [LoggerMessage(0, LogLevel.Debug, "[{LogKey}] query cache behavior hit (key={CacheKey}, type={BehaviorType})")]
        public static partial void LogCacheHit(ILogger logger, string logKey, string cacheKey, string behaviorType);

        /// <summary>
        /// Writes a log entry for the cache miss operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="cacheKey">The cache key used by the operation.</param>
        /// <param name="behaviorType">The behavior type used by the operation.</param>
        [LoggerMessage(1, LogLevel.Debug, "[{LogKey}] query cache behavior miss (key={CacheKey}, type={BehaviorType})")]
        public static partial void LogCacheMiss(ILogger logger, string logKey, string cacheKey, string behaviorType);

        /// <summary>
        /// Writes a log entry for the cache add operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="cacheKey">The cache key used by the operation.</param>
        /// <param name="behaviorType">The behavior type used by the operation.</param>
        [LoggerMessage(2, LogLevel.Debug, "[{LogKey}] query cache behavior set (key={CacheKey}, type={BehaviorType})")]
        public static partial void LogCacheAdd(ILogger logger, string logKey, string cacheKey, string behaviorType);
    }
}
