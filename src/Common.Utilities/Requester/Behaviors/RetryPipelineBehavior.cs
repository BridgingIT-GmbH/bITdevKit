// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

public class RetryPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    IOptions<RetryOptions> options = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly RetryOptions retryOptions = options?.Value ?? new RetryOptions();

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

        // Use attribute values if specified, otherwise fall back to options defaults
        var retryCount = policyConfig.Retry.Count ?? this.retryOptions.DefaultCount;
        var delayMs = policyConfig.Retry.Delay ?? this.retryOptions.DefaultDelay;

        if (!retryCount.HasValue || !delayMs.HasValue)
        {
            this.Logger.LogError("{LogKey} retry behavior: count or delay not specified on attribute and no default configured via RetryOptions (handler={HandlerType})", LogKey, handlerType.FullName);
            throw new InvalidOperationException("HandlerRetryAttribute.Count and Delay must be provided or default values must be configured via RetryOptions.");
        }

        var delay = TimeSpan.FromMilliseconds(delayMs.Value);

        var policy = Policy<TResponse>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount.Value,
                retryAttempt => delay,
                (exception, timespan, retryAttempt, context) => this.Logger.LogWarning("{LogKey} retry behavior attempt {RetryAttempt} of {RetryCount} for {HandlerType} after {DelayMs} ms due to {ExceptionMessage}", LogKey, retryAttempt, retryCount, handlerType.Name, timespan.TotalMilliseconds, exception.Exception?.Message));

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }

    public override bool IsHandlerSpecific()
    {
        return true;
    }
}