// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

public class TimeoutPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    IOptions<TimeoutOptions> options = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly TimeoutOptions timeoutOptions = options?.Value ?? new TimeoutOptions();

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.Timeout != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.Timeout == null)
        {
            return await next();
        }

        // Use attribute value if specified, otherwise fall back to options default
        var durationMs = policyConfig.Timeout.Duration ?? this.timeoutOptions.DefaultDuration;

        if (!durationMs.HasValue)
        {
            this.Logger.LogError("{LogKey} timeout behavior: duration not specified on attribute and no default configured via TimeoutOptions (handler={HandlerType})", LogKey, handlerType.FullName);
            throw new InvalidOperationException("HandlerTimeoutAttribute.Duration must be provided or a default value must be configured via TimeoutOptions.");
        }

        var timeout = TimeSpan.FromMilliseconds(durationMs.Value);
        var policy = Policy.TimeoutAsync<TResponse>(
            timeout,
            TimeoutStrategy.Pessimistic,
            (context, timespan, task, exception) =>
            {
                this.Logger.LogWarning("{LogKey} timeout behavior triggered (timeout={TimeoutMs} ms, type={BehaviorType})", LogKey, timespan.TotalMilliseconds, this.GetType().Name);
                return Task.CompletedTask;
            });

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }

    /// <summary>
    /// Indicates that this behavior is handler-specific and should run for each handler.
    /// </summary>
    /// <returns><c>true</c> to indicate this is a handler-specific behavior.</returns>
    public override bool IsHandlerSpecific()
    {
        return true;
    }
}