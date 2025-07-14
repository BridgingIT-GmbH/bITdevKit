// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polly;

public class CircuitBreakerPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.CircuitBreaker != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.CircuitBreaker == null)
        {
            return await next();
        }

        var options = policyConfig.CircuitBreaker;
        var attempts = 1;

        var retryPolicy = options.BackoffExponential
            ? Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => TimeSpan.FromMilliseconds(options.Backoff.Milliseconds * Math.Pow(2, attempt)),
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attempts, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                        attempts++;
                    })
            : Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => options.Backoff,
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attempts, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                        attempts++;
                    });

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                options.Attempts,
                options.BreakDuration,
                (ex, wait) => this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (circuit=open, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, wait.TotalMilliseconds, this.GetType().Name, ex.Message),
                () => this.Logger.LogDebug("{LogKey} circuit breaker behavior (circuit=closed, type={BehaviorType})", LogKey, this.GetType().Name),
                () => this.Logger.LogDebug("{LogKey} circuit breaker behavior (circuit=halfopen, type={BehaviorType})", LogKey, this.GetType().Name));

        var policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }
}
