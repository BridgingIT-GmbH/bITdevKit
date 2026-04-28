// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

/// <summary>
///     Configuration values for binding the RabbitMQ queue broker from application settings.
/// </summary>
public class RabbitMQQueueBrokerConfiguration
{
    /// <summary>
    ///     Gets or sets the RabbitMQ host name.
    /// </summary>
    public string HostName { get; set; }

    /// <summary>
    ///     Gets or sets the RabbitMQ connection string.
    /// </summary>
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
    public int? PrefetchCount { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether queues and messages are durable.
    /// </summary>
    public bool? IsDurable { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether queues are auto-deleted.
    /// </summary>
    public bool? AutoDeleteQueue { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether queues are exclusive.
    /// </summary>
    public bool? ExclusiveQueue { get; set; }

    /// <summary>
    ///     Gets or sets the default message expiration.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    ///     Gets or sets the maximum delivery attempts before a message is dead-lettered.
    /// </summary>
    public int? MaxDeliveryAttempts { get; set; }

    /// <summary>
    ///     Gets or sets the processing delay in milliseconds.
    /// </summary>
    public int? ProcessDelay { get; set; }
}
