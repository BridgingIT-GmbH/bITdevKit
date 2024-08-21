// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

public class DummyMessageHandlerBehavior(ILoggerFactory loggerFactory) : MessageHandlerBehaviorBase(loggerFactory)
{
    public override async Task Handle<TMessage>(TMessage message, CancellationToken cancellationToken, object handler, MessageHandlerDelegate next)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        this.Logger.LogDebug("{LogKey} >>>>> dummy message handle behavior - before (id={MessageId})", Constants.LogKey, message.MessageId);

        await next().AnyContext(); // continue pipeline

        this.Logger.LogDebug("{LogKey} <<<<< dummy message handle behavior - after (id={MessageId})", Constants.LogKey, message.MessageId);
    }
}