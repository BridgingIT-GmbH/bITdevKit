// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using Application.Messaging;
using Common;

/// <summary>
/// Configures rabbit mq message broker.
/// </summary>
public class RabbitMQMessageBrokerOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the publisher behaviors.
    /// </summary>
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler behaviors.
    /// </summary>
    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler factory.
    /// </summary>
    public IMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the serializer.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets the host name.
    /// </summary>
    public string HostName { get; set; } //= "localhost";

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; } // see https://www.rabbitmq.com/uri-spec.html

    /// <summary>
    /// Gets or sets the exchange name.
    /// </summary>
    public string ExchangeName { get; set; } = "messaging";

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; set; } //= "shared"; // =module name

    /// <summary>
    /// Gets or sets the queue name suffix.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    /// Gets or sets the retries.
    /// </summary>
    public int Retries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the process delay.
    /// </summary>
    public int ProcessDelay { get; set; } = 100;

    /// <summary>
    ///     The default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    ///     Durable queue, survives a broker restart
    /// </summary>
    public bool IsDurable { get; set; } = false;

    /// <summary>
    ///     Queue is exclusive to the message broker
    /// </summary>
    public bool ExclusiveQueue { get; set; } = true;

    /// <summary>
    ///     Queue should be deleted automatically
    /// </summary>
    public bool AutoDeleteQueue { get; set; } = true;
}
