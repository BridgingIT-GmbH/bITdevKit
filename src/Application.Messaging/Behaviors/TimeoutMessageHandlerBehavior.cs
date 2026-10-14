// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Humanizer;
using Polly;
using Polly.Timeout;

/// <summary>
/// Provides timeout message handler behavior.
/// </summary>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class TimeoutMessageHandlerBehavior(ILoggerFactory loggerFactory) : MessageHandlerBehaviorBase(loggerFactory)
{
    /// <inheritdoc/>
    public override async Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = (handler as ITimeoutMessageHandler)?.Options;
        if (options is not null)
        {
            var pipeline = new ResiliencePipelineBuilder()
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = options.Timeout,
                    OnTimeout = args =>
                    {
                        this.Logger.LogError(
                            $"{{LogKey}} message handler behavior timeout (timeout={args.Timeout.Humanize()}, type={this.GetType().Name})",
                            Constants.LogKey);
                        return default;
                    }
                })
                .Build();

            // WaitAsync abandons a handler that ignores cancellation, so the timeout is enforced pessimistically
            await pipeline.ExecuteAsync(async token => await next().WaitAsync(token).AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}
