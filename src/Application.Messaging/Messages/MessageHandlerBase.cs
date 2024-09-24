// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public abstract class MessageHandlerBase<TMessage> : IMessageHandler<TMessage>
    where TMessage : IMessage
{
    protected MessageHandlerBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public abstract Task Handle(TMessage message, CancellationToken cancellationToken);
}