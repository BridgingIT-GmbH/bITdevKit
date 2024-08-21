// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

// generate a xunit test for this class and its methods, use nsubstitute and shoulpublic class DummyMessagePublisherBehaviorTests

public class DummyMessagePublisherBehavior(ILoggerFactory loggerFactory) : MessagePublisherBehaviorBase(loggerFactory)
{
    public override async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next)
    {
        this.Logger.LogDebug("{LogKey} >>>>> dummy message publish behavior - before (id={MessageId})", Constants.LogKey, message.MessageId);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await next().AnyContext(); // continue pipeline

        this.Logger.LogDebug("{LogKey} <<<<< dummy message publish behavior - after (id={MessageId})", Constants.LogKey, message.MessageId);
    }
}