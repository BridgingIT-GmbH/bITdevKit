// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Humanizer;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public class CircuitBreakerQueryBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory) : QueryBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, IRequest<TResponse>
{
    protected override bool CanProcess(TRequest request)
    {
        return request is ICircuitBreakerQuery;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // retry only if implements interface
        if (request is not ICircuitBreakerQuery instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.Attempts <= 0)
        {
            instance.Options.Attempts = 1;
        }

        var attempts = 1;
        AsyncRetryPolicy retryPolicy;
        if (!instance.Options.BackoffExponential)
        {
            retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(
                    instance.Options.Backoff != default
                        ? instance.Options.Backoff.Milliseconds
                        : 0),
                onRetry: (ex, wait) =>
                {
                    this.Logger.LogError(ex, "{LogKey} query circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, type={BehaviorType}) {ErrorMessage}", Constants.LogKey, attempts, wait.Humanize(), this.GetType().Name, ex.Message);
                    attempts++;
                });
        }
        else
        {
            retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                attempt => TimeSpan.FromMilliseconds(
                    instance.Options.Backoff != default
                        ? instance.Options.Backoff.Milliseconds
                        : 0
                    * Math.Pow(2, attempt)),
                (ex, wait) =>
                {
                    this.Logger.LogError(ex, "{LogKey} query circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, type={BehaviorType}) {ErrorMessage}", Constants.LogKey, attempts, wait.Humanize(), this.GetType().Name, ex.Message);
                    attempts++;
                });
        }

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: instance.Options.Attempts,
                durationOfBreak: instance.Options.BreakDuration.TotalMilliseconds >= 0
                    ? instance.Options.BreakDuration
                    : new TimeSpan(0, 0, 30),
                onBreak: (ex, wait) =>
                {
                    this.Logger.LogError(ex, "{LogKey} query circuitbreaker behavior (circuit=open, wait={Wait}, type={BehaviorType}) {ErrorMessage}", Constants.LogKey, wait.Humanize(), this.GetType().Name, ex.Message);
                },
                onReset: () => this.Logger.LogDebug("{LogKey} query circuitbreaker behavior (circuit=closed, type={BehaviorType})", Constants.LogKey, this.GetType().Name),
                onHalfOpen: () => this.Logger.LogDebug("{LogKey} query circuitbreaker behavior (circuit=halfopen, type={BehaviorType})", Constants.LogKey, this.GetType().Name));

        var policyWrap = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        return await policyWrap.ExecuteAsync(async (context) => await next().AnyContext(), cancellationToken);
    }
}