// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

public class CircuitBreakerPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    IOptions<CircuitBreakerOptions> options = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly CircuitBreakerOptions circuitBreakerOptions = options?.Value ?? new CircuitBreakerOptions();

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

        var attr = policyConfig.CircuitBreaker;

        // Use attribute values if specified, otherwise fall back to options defaults
        var attempts = attr.Attempts ?? this.circuitBreakerOptions.DefaultAttempts;
        var breakDurationSeconds = attr.BreakDurationSeconds ?? this.circuitBreakerOptions.DefaultBreakDurationSeconds;
        var backoffMilliseconds = attr.BackoffMilliseconds ?? this.circuitBreakerOptions.DefaultBackoffMilliseconds;
        var backoffExponential = attr.BackoffExponential ?? this.circuitBreakerOptions.DefaultBackoffExponential;

        if (!attempts.HasValue || !breakDurationSeconds.HasValue || !backoffMilliseconds.HasValue || !backoffExponential.HasValue)
        {
            this.Logger.LogError("{LogKey} circuit breaker behavior: required parameters not specified on attribute and no defaults configured via CircuitBreakerOptions (handler={HandlerType})", LogKey, handlerType.FullName);
            throw new InvalidOperationException("HandlerCircuitBreakerAttribute parameters (Attempts, BreakDurationSeconds, BackoffMilliseconds, BackoffExponential) must be provided or default values must be configured via CircuitBreakerOptions.");
        }

        var breakDuration = TimeSpan.FromSeconds(breakDurationSeconds.Value);
        var backoff = TimeSpan.FromMilliseconds(backoffMilliseconds.Value);
        var attemptCounter = 1;

        var retryPolicy = backoffExponential.Value
            ? Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => TimeSpan.FromMilliseconds(backoff.Milliseconds * Math.Pow(2, attempt)),
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attemptCounter, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                        attemptCounter++;
                    })
            : Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => backoff,
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attemptCounter, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                        attemptCounter++;
                    });

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                attempts.Value,
                breakDuration,
                (ex, wait) => this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (circuit=open, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, wait.TotalMilliseconds, this.GetType().Name, ex.Message),
                () => this.Logger.LogDebug("{LogKey} circuit breaker behavior (circuit=closed, type={BehaviorType})", LogKey, this.GetType().Name),
                () => this.Logger.LogDebug("{LogKey} circuit breaker behavior (circuit=halfopen, type={BehaviorType})", LogKey, this.GetType().Name));

        var policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }
}