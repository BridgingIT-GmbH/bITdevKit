// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using MediatR;
using Microsoft.Extensions.Logging;

public partial class CacheQueryBehavior<TRequest, TResponse> : QueryBehaviorBase<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly ICacheProvider provider;

    public CacheQueryBehavior(ILoggerFactory loggerFactory, ICacheProvider provider)
        : base(loggerFactory)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override bool CanProcess(TRequest request)
    {
        return request is ICacheQuery;
    }

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

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} query cache behavior hit (key={CacheKey}, type={BehaviorType})")]
        public static partial void LogCacheHit(ILogger logger, string logKey, string cacheKey, string behaviorType);

        [LoggerMessage(1, LogLevel.Debug, "{LogKey} query cache behavior miss (key={CacheKey}, type={BehaviorType})")]
        public static partial void LogCacheMiss(ILogger logger, string logKey, string cacheKey, string behaviorType);

        [LoggerMessage(2, LogLevel.Debug, "{LogKey} query cache behavior set (key={CacheKey}, type={BehaviorType})")]
        public static partial void LogCacheAdd(ILogger logger, string logKey, string cacheKey, string behaviorType);
    }
}