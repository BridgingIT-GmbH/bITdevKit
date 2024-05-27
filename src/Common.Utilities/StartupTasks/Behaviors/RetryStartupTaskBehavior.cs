// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Polly.Retry;
using Polly; // TODO: migrate to Polly 8 https://www.pollydocs.org/migration-v8.html
using Humanizer;
using System.Diagnostics;

public class RetryStartupTaskBehavior(ILoggerFactory loggerFactory) : StartupTaskBehaviorBase(loggerFactory)
{
    public override async Task Execute(IStartupTask task, CancellationToken cancellationToken, TaskDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (task as IRetryStartupTask)?.Options;
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
                retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(
                    options.Attempts,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(
                        options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0),
                    onRetry: (ex, wait) =>
                    {
                        Activity.Current?.AddEvent(new($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                        this.Logger.LogError(ex, $"{{LogKey}} startup task retry behavior (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}", "UTL");
                        attempts++;
                    });
            }
            else
            {
                retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(
                    options.Attempts,
                    attempt => TimeSpan.FromMilliseconds(
                        options.Backoff != default
                            ? options.Backoff.Milliseconds
                            : 0
                        * Math.Pow(2, attempt)),
                    (ex, wait) =>
                    {
                        Activity.Current?.AddEvent(new($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                        this.Logger.LogError(ex, $"{{LogKey}} startup task retry behavior (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}", "UTL");
                        attempts++;
                    });
            }

            await retryPolicy.ExecuteAsync(async (context) => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}