// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;
using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;

public class RabbitMQMessageBrokerOptions : OptionsBase
{
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    public IMessageHandlerFactory HandlerFactory { get; set; }

    public ISerializer Serializer { get; set; }

    public string HostName { get; set; } //= "localhost";

    public string ConnectionString { get; set; } // see https://www.rabbitmq.com/uri-spec.html

    public string ExchangeName { get; set; } = "messaging";

    public string QueueName { get; set; } //= "shared"; // =module name

    public string QueueNameSuffix { get; set; }

    public int Retries { get; set; } = 3;

    public int ProcessDelay { get; set; } = 100;

    /// <summary>
    /// The default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    /// Durable queue, survives a broker restart
    /// </summary>
    public bool IsDurable { get; set; } = false;

    /// <summary>
    /// Queue is exclusive to the message broker
    /// </summary>
    public bool ExclusiveQueue { get; set; } = true;

    /// <summary>
    /// Queue should be deleted automatically
    /// </summary>
    public bool AutoDeleteQueue { get; set; } = true;
}