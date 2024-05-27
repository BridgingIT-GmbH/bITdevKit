// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Humanizer;
using Microsoft.Extensions.Logging;
using Polly; // TODO: migrate to Polly 8 https://www.pollydocs.org/migration-v8.html
using Polly.Retry;

public class CircuitBreakerStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (task as ICircuitBreakerStartupTask)?.Options;
        if (options is not null)
        {
            if (options.Attempts <= 0)
            {
                options.Attempts = 1;
            }

            var attempts = 1;
            AsyncRetryPolicy retryPolicy;
            if (!options.BackoffExponential)
            {
                retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(
                        options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0),
                    onRetry: (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} startup task circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, type={BehaviorType}) {ErrorMessage}", "UTL", attempts, wait.Humanize(), this.GetType().Name, ex.Message);
                        attempts++;
                    });
            }
            else
            {
                retryPolicy = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                    attempt => TimeSpan.FromMilliseconds(
                        options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0
                        * Math.Pow(2, attempt)),
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} startup task circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, type={BehaviorType}) {ErrorMessage}", "UTL", attempts, wait.Humanize(), this.GetType().Name, ex.Message);
                        attempts++;
                    });
            }

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: options.Attempts,
                    durationOfBreak: options.BreakDuration.TotalMilliseconds >= 0
                        ? options.BreakDuration
                        : new TimeSpan(0, 0, 30),
                    onBreak: (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} startup task circuitbreaker behavior (circuit=open, wait={Wait}, type={BehaviorType}) {ErrorMessage}", "UTL", wait.Humanize(), this.GetType().Name, ex.Message);
                    },
                    onReset: () => this.Logger.LogDebug("{LogKey} startup task circuitbreaker behavior (circuit=closed, type={BehaviorType})", "UTL", this.GetType().Name),
                    onHalfOpen: () => this.Logger.LogDebug("{LogKey} startup task circuitbreaker behavior (circuit=halfopen, type={BehaviorType})", "UTL", this.GetType().Name));

            var policyWrap = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

            await policyWrap.ExecuteAsync(async (context) => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}