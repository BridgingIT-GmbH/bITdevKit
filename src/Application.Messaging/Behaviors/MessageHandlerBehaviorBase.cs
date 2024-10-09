// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public abstract class MessageHandlerBehaviorBase : IMessageHandlerBehavior
{
    protected MessageHandlerBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public abstract Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next)
        where TMessage : IMessage;
}