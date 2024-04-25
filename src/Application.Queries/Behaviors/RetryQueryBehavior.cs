// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Humanizer;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly; // TODO: migrate to Polly 8 https://www.pollydocs.org/migration-v8.html
using Polly.Retry;

public class RetryQueryBehavior<TRequest, TResponse> : QueryBehaviorBase<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    public RetryQueryBehavior(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override bool CanProcess(TRequest request)
    {
        return request is IRetryQuery;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // retry only if implements interface
        if (request is not IRetryQuery instance)
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
            retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(
                instance.Options.Attempts,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(
                    instance.Options.Backoff != default
                        ? instance.Options.Backoff.Milliseconds
                        : 0),
                onRetry: (ex, wait) =>
            {
                Activity.Current?.AddEvent(new($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                this.Logger.LogError(ex, $"{{LogKey}} query retry behavior (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}", Constants.LogKey);
                attempts++;
            });
        }
        else
        {
            retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(
                instance.Options.Attempts,
                attempt => TimeSpan.FromMilliseconds(
                    instance.Options.Backoff != default
                        ? instance.Options.Backoff.Milliseconds
                        : 0
                    * Math.Pow(2, attempt)),
                (ex, wait) =>
                {
                    Activity.Current?.AddEvent(new($"Retry (attempt=#{attempts}, type={this.GetType().Name}) {ex.Message}"));
                    this.Logger.LogError(ex, $"{{LogKey}} query retry behavior (attempt=#{attempts}, wait={wait.Humanize()}, type={this.GetType().Name}) {ex.Message}", Constants.LogKey);
                    attempts++;
                });
        }

        return await retryPolicy.ExecuteAsync(async (context) => await next().AnyContext(), cancellationToken);
    }
}