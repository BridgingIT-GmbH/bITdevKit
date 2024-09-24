// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
///     Describes the interface of the messagebus.
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    ///     Subscribes for the message (TMessage) with a specific message handler (THandler).
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="THandler">The type of the message handler.</typeparam>
    Task Subscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>;

    /// <summary>
    ///     Subscribes for the message with a specific message handler.
    /// </summary>
    /// <typeparam name="messageType">The type of the message.</typeparam>
    /// <typeparam name="handlerType">The type of the message handler.</typeparam>
    Task Subscribe(Type messageType, Type handlerType);

    /// <summary>
    ///     Unsubscribes message (TMessage) and its message handler (THandler).
    /// </summary>
    /// <typeparam name="TMessage">The type of the message.</typeparam>
    /// <typeparam name="THandler">The type of the message handler.</typeparam>
    Task Unsubscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>;

    /// <summary>
    ///     Unsubscribes message and its message handler.
    /// </summary>
    /// <typeparam name="messageType">The type of the message.</typeparam>
    /// <typeparam name="handlerType">The type of the message handler.</typeparam>
    Task Unsubscribe(Type messageType, Type handlerType);

    /// <summary>
    ///     Unsubscribes all messages and its message handlers.
    /// </summary>
    Task Unsubscribe();

    /// <summary>
    ///     Publishes the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    Task Publish(IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Process a subscribed message
    /// </summary>
    Task Process(MessageRequest messageRequest);
}