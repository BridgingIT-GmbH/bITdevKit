﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Humanizer;
using Polly;
using Polly.Timeout;

public class TimeoutMessageHandlerBehavior(ILoggerFactory loggerFactory) : MessageHandlerBehaviorBase(loggerFactory)
{
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
            var timeoutPolicy = Policy.TimeoutAsync(options.Timeout,
                TimeoutStrategy.Pessimistic,
                async (context, timeout, task) =>
                {
                    await Task.Run(() =>
                        this.Logger.LogError(
                            $"{{LogKey}} message handler behavior timeout (timeout={timeout.Humanize()}, type={this.GetType().Name})",
                            Constants.LogKey));
                });

            await timeoutPolicy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}