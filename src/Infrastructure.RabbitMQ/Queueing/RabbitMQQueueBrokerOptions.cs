// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using Application.Queueing;
using Common;

/// <summary>
///     Options for configuring the <see cref="RabbitMQQueueBroker" />.
/// </summary>
public class RabbitMQQueueBrokerOptions : OptionsBase
{
    /// <summary>
    ///     Gets or sets the enqueuer behaviors.
    /// </summary>
    public IEnumerable<IQueueEnqueuerBehavior> EnqueuerBehaviors { get; set; }

    /// <summary>
    ///     Gets or sets the handler behaviors.
    /// </summary>
    public IEnumerable<IQueueHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    ///     Gets or sets the handler factory.
    /// </summary>
    public IQueueMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    ///     Gets or sets the serializer.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    ///     Gets or sets the RabbitMQ host name.
    /// </summary>
    public string HostName { get; set; }

    /// <summary>
    ///     Gets or sets the RabbitMQ connection string.
    /// </summary>
    /// <remarks>See https://www.rabbitmq.com/uri-spec.html for URI format.</remarks>
    public string ConnectionString { get; set; }

    /// <summary>
    ///     Gets or sets the queue name prefix.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    ///     Gets or sets the queue name suffix.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    ///     Gets or sets the prefetch count for consumers.
    /// </summary>
    /// <remarks>Defaults to 20.</remarks>
    public int PrefetchCount { get; set; } = 20;

    /// <summary>
    ///     Gets or sets a value indicating whether queues and messages are durable.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool IsDurable { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether queues are auto-deleted.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>.</remarks>
    public bool AutoDeleteQueue { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether queues are exclusive.
    /// </summary>
    /// <remarks>Defaults to <c>false</c>. Set to <c>true</c> only for single-instance scenarios.</remarks>
    public bool ExclusiveQueue { get; set; } = false;

    /// <summary>
    ///     Gets or sets the default message expiration.
    /// </summary>
    /// <remarks>Defaults to 7 days.</remarks>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    ///     Gets or sets the maximum delivery attempts before a message is dead-lettered.
    /// </summary>
    /// <remarks>Defaults to 5.</remarks>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the processing delay before invoking the handler.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public int ProcessDelay { get; set; } = 0;
}
