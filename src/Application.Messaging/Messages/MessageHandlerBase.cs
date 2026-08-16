// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents message handler base.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public abstract class MessageHandlerBase<TMessage> : IMessageHandler<TMessage>
    where TMessage : IMessage
{
    /// <summary>
    /// Initializes a new instance of the <c>MessageHandlerBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    protected MessageHandlerBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Handles .
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task Handle(TMessage message, CancellationToken cancellationToken);
}
