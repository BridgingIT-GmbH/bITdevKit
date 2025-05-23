﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

public class ChaosExceptionMessageHandlerBehavior(ILoggerFactory loggerFactory)
    : MessageHandlerBehaviorBase(loggerFactory)
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

        var options = (handler as IChaosExceptionMessageHandler)?.Options;
        if (options?.InjectionRate > 0)
        {
            // https://github.com/Polly-Contrib/Simmy#Inject-exception
            var policy = MonkeyPolicy.InjectException(with =>
                with.Fault(options.Fault ?? new ChaosException()).InjectionRate(options.InjectionRate).Enabled());

            await policy.Execute(async context => await next().AnyContext(), cancellationToken);
        }
        else
        {
            await next().AnyContext(); // continue pipeline
        }
    }
}