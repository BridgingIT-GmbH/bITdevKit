// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Messaging;
using Common;

public class ServiceBusMessageBrokerOptions : OptionsBase
{
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    public IMessageHandlerFactory HandlerFactory { get; set; }

    public ISerializer Serializer { get; set; }

    public string ConnectionString { get; set; }

    public string TopicScope { get; set; }

    public int Retries { get; set; } = 3;

    public int ProcessDelay { get; set; } = 100;

    /// <summary>
    ///     The default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }
}