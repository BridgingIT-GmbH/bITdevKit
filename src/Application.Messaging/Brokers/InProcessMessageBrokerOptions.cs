// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using BridgingIT.DevKit.Common;

public class InProcessMessageBrokerOptions : OptionsBase
{
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    public IMessageHandlerFactory HandlerFactory { get; set; }

    public ISerializer Serializer { get; set; }

    public string FilterScope { get; set; }

    public string MessageScope { get; set; } = "local";

    public int ProcessDelay { get; set; } = 100; // milliseconds

    public TimeSpan? MessageExpiration { get; set; }
}