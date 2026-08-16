// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents message publisher behavior base.
/// </summary>
public abstract class MessagePublisherBehaviorBase : IMessagePublisherBehavior
{
    /// <summary>
    /// Initializes a new instance of the <c>MessagePublisherBehaviorBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    protected MessagePublisherBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Publishes .
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task Publish<TMessage>(
        TMessage message,
        CancellationToken cancellationToken,
        MessagePublisherDelegate next)
        where TMessage : IMessage;
}
