// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polly;

public class RetryPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return true;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.Retry == null)
        {
            return await next();
        }

        var retryCount = policyConfig.Retry.Count;
        var delay = TimeSpan.FromMilliseconds(policyConfig.Retry.Delay);

        var policy = Policy<TResponse>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => delay,
                (exception, timespan, retryAttempt, context) => this.Logger.LogWarning("{LogKey} retry behavior attempt {RetryAttempt} of {RetryCount} for {HandlerType} after {DelayMs} ms due to {ExceptionMessage}", LogKey, retryAttempt, retryCount, handlerType.Name, timespan.TotalMilliseconds, exception.Exception?.Message));

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }

    public override bool IsHandlerSpecific()
    {
        return true;
    }
}
