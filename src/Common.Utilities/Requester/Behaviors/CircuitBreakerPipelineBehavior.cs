// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

/// <summary>
///     Applies retry and circuit-breaker policies configured for a request handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The result response type.</typeparam>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
/// <param name="policyCache">The handler policy cache.</param>
/// <param name="options">Default circuit-breaker settings used when an attribute omits values.</param>
public class CircuitBreakerPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    IOptions<CircuitBreakerOptions> options = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private static readonly TimeSpan MinBreakDuration = TimeSpan.FromMilliseconds(500);
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly CircuitBreakerOptions circuitBreakerOptions = options?.Value ?? new CircuitBreakerOptions();

    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.CircuitBreaker != null;
    }

    /// <inheritdoc/>
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
            this.Logger.LogError("[{LogKey}] circuit breaker behavior: required parameters not specified on attribute and no defaults configured via CircuitBreakerOptions (handler={HandlerType})", LogKey, handlerType.FullName);
            throw new InvalidOperationException("HandlerCircuitBreakerAttribute parameters (Attempts, BreakDurationSeconds, BackoffMilliseconds, BackoffExponential) must be provided or default values must be configured via CircuitBreakerOptions.");
        }

        var breakDuration = TimeSpan.FromSeconds(breakDurationSeconds.Value);
        var backoff = TimeSpan.FromMilliseconds(backoffMilliseconds.Value);
        var attemptCounter = 1;

        // Polly v8 has no consecutive-failure breaker: a failure ratio of 100% over a sampling window
        // spanning the whole execution breaks after N consecutive failures within this execution
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = int.MaxValue,
                Delay = backoff,
                BackoffType = backoffExponential.Value ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                UseJitter = false,
                OnRetry = args =>
                {
                    this.Logger.LogError(args.Outcome.Exception, "[{LogKey}] circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attemptCounter, args.RetryDelay.TotalMilliseconds, this.GetType().Name, args.Outcome.Exception?.Message);
                    attemptCounter++;
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 1.0,
                MinimumThroughput = Math.Max(2, attempts.Value),
                SamplingDuration = TimeSpan.FromDays(1),
                BreakDuration = breakDuration < MinBreakDuration ? MinBreakDuration : breakDuration,
                OnOpened = args =>
                {
                    this.Logger.LogError(args.Outcome.Exception, "[{LogKey}] circuit breaker behavior (circuit=open, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, args.BreakDuration.TotalMilliseconds, this.GetType().Name, args.Outcome.Exception?.Message);
                    return default;
                },
                OnClosed = args =>
                {
                    this.Logger.LogDebug("[{LogKey}] circuit breaker behavior (circuit=closed, type={BehaviorType})", LogKey, this.GetType().Name);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    this.Logger.LogDebug("[{LogKey}] circuit breaker behavior (circuit=halfopen, type={BehaviorType})", LogKey, this.GetType().Name);
                    return default;
                }
            })
            .Build();

        return await pipeline.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken).AnyContext();
    }
}
