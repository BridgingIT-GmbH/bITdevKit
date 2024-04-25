// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using Humanizer;
using Microsoft.Extensions.Logging;
using Polly; // TODO: migrate to Polly 8 https://www.pollydocs.org/migration-v8.html
using Polly.Retry;
using System.Diagnostics;

public class RetryMessageHandlerBehavior : MessageHandlerBehaviorBase
{
    public RetryMessageHandlerBehavior(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    public override async Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (handler as IRetryMessageHandler)?.Options;
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
                        this.Logger.LogError(ex, $"{{LogKey}} message handler retry behavior (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}", Constants.LogKey);
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
                        this.Logger.LogError(ex, $"{{LogKey}} message handler retry behavior (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}", Constants.LogKey);
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
