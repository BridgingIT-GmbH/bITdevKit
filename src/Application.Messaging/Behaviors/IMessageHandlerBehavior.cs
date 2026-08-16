// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents task.
/// </summary>
/// <returns>The value returned by the delegate.</returns>
public delegate Task MessageHandlerDelegate();

/// <summary>
/// Defines operations for i message handler behavior.
/// </summary>
public interface IMessageHandlerBehavior
{
    /// <summary>
    /// Handles .
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="handler">The handler used by the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Handle<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        object handler,
        MessageHandlerDelegate next)
        where TMessage : IMessage;
}
