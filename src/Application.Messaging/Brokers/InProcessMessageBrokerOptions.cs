// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Configures the in-process message broker.
/// </summary>
public class InProcessMessageBrokerOptions : OptionsBase
{
    /// <summary>Gets or sets the behaviors applied while publishing messages.</summary>
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    /// <summary>Gets or sets the behaviors applied while handling messages.</summary>
    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>Gets or sets the factory used to resolve message handlers.</summary>
    public IMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>Gets or sets the serializer used for message payloads.</summary>
    public ISerializer Serializer { get; set; }

    /// <summary>Gets or sets the optional scope used to filter messages.</summary>
    public string FilterScope { get; set; }

    /// <summary>Gets or sets the message scope. The default is <c>local</c>.</summary>
    public string MessageScope { get; set; } = "local";

    /// <summary>Gets or sets the delay between processing attempts, in milliseconds.</summary>
    public int ProcessDelay { get; set; } = 100; // milliseconds

    /// <summary>Gets or sets the optional duration after which a message expires.</summary>
    public TimeSpan? MessageExpiration { get; set; }
}
