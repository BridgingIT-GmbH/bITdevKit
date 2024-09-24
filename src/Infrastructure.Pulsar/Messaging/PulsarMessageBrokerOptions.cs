// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Pulsar;

using Application.Messaging;
using Common;

public class PulsarMessageBrokerOptions : OptionsBase
{
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    public IMessageHandlerFactory HandlerFactory { get; set; }

    public ISerializer Serializer { get; set; }

    public TimeSpan? RetryInterval { get; set; }

    public TimeSpan? KeepAliveInterval { get; set; }

    public string ServiceUrl { get; set; }

    public string Subscription { get; set; } = "default";

    public string MessageScope { get; set; } = "local";

    public int ProcessDelay { get; set; } = 100;
}