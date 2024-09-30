// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Humanizer;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public class CircuitBreakerStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var taskName = task.GetType().PrettyName();
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
                retryPolicy = Policy.Handle<Exception>()
                    .WaitAndRetryForeverAsync(attempt => TimeSpan.FromMilliseconds(options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0),
                        (ex, wait) =>
                        {
                            this.Logger.LogError(ex,
                                "{LogKey} startup task circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, task={StartupTaskType}) {ErrorMessage}",
                                "UTL",
                                attempts,
                                wait.Humanize(),
                                taskName,
                                ex.Message);
                            attempts++;
                        });
            }
            else
            {
                retryPolicy = Policy.Handle<Exception>()
                    .WaitAndRetryForeverAsync(attempt => TimeSpan.FromMilliseconds(options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0 * Math.Pow(2, attempt)),
                        (ex, wait) =>
                        {
                            this.Logger.LogError(ex,
                                "{LogKey} startup task circuitbreaker behavior (attempt=#{Attempts}, wait={Wait}, task={StartupTaskType}) {ErrorMessage}",
                                "UTL",
                                attempts,
                                wait.Humanize(),
                                this.GetType().Name,
                                ex.Message);
                            attempts++;
                        });
            }

            var circuitBreakerPolicy = Policy.Handle<Exception>()
                .CircuitBreakerAsync(options.Attempts,
                    options.BreakDuration.TotalMilliseconds >= 0 ? options.BreakDuration : new TimeSpan(0, 0, 30),
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex,
                            "{LogKey} startup task circuitbreaker behavior (circuit=open, wait={Wait}, task={StartupTaskType}) {ErrorMessage}",
                            "UTL",
                            wait.Humanize(),
                            taskName,
                            ex.Message);
                    },
                    () => this.Logger.LogDebug(
                        "{LogKey} startup task circuitbreaker behavior (circuit=closed, task={StartupTaskType})",
                        "UTL",
                        taskName),
                    () => this.Logger.LogDebug(
                        "{LogKey} startup task circuitbreaker behavior (circuit=halfopen, task={StartupTaskType})",
                        "UTL",
                        taskName));

            var policyWrap = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

            await policyWrap.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}