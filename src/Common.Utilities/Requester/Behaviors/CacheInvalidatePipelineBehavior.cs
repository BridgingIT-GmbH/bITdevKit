// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public class CacheInvalidatePipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    ICacheProvider provider) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly ICacheProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.CacheInvalidate != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.CacheInvalidate == null)
        {
            return await next();
        }

        var key = policyConfig.CacheInvalidate.Key;
        if (string.IsNullOrEmpty(key))
        {
            return await next();
        }

        var result = await next(); // Continue pipeline

        this.Logger.LogDebug("{LogKey} cache invalidate behavior (key={CacheKey}*, type={BehaviorType})", LogKey, key, this.GetType().Name);
        await this.provider.RemoveStartsWithAsync(key, cancellationToken);

        return result;
    }
}
