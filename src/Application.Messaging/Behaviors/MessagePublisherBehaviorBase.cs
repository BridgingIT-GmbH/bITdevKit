// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public abstract class MessagePublisherBehaviorBase : IMessagePublisherBehavior
{
    protected MessagePublisherBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public abstract Task Publish<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        MessagePublisherDelegate next)
        where TMessage : IMessage;
}