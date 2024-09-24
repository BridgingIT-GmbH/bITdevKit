// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public interface IMessageHandler<TMessage>
    where TMessage : IMessage
{
    /// <summary>
    ///     Handles the specified message.
    /// </summary>
    /// <param name="message">The event.</param>
    Task Handle(TMessage message, CancellationToken cancellationToken);
}