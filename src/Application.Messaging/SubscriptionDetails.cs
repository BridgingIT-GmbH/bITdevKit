// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public class SubscriptionDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionDetails"/> class.
    /// </summary>
    /// <param name="handlerType">Type of the handler.</param>
    private SubscriptionDetails(Type messageType, Type handlerType)
    {
        this.MessageType = messageType;
        this.HandlerType = handlerType;
    }

    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    /// <value>
    /// The type of the handler.
    /// </value>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the type of the handler.
    /// </summary>
    /// <value>
    /// The type of the handler.
    /// </value>
    public Type HandlerType { get; }

    /// <summary>
    /// Creates a <see cref="SubscriptionDetails"/> for specified message/handler types.
    /// </summary>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="handlerType">Type of the handler.</param>
    public static SubscriptionDetails Create(Type messageType, Type handlerType)
    {
        EnsureArg.IsNotNull(messageType, nameof(messageType));
        EnsureArg.IsNotNull(handlerType, nameof(handlerType));

        return new SubscriptionDetails(messageType, handlerType);
    }
}