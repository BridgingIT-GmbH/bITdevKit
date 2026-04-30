// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

/// <summary>
/// Represents the runtime options used by <see cref="ServiceBusQueueBroker"/>.
/// </summary>
/// <example>
/// <code>
/// var options = new ServiceBusQueueBrokerOptions
/// {
///     ConnectionString = "Endpoint=sb://...",
///     Serializer = new SystemTextJsonSerializer(),
///     MaxDeliveryAttempts = 5,
///     MaxConcurrentCalls = 8
/// };
/// </code>
/// </example>
public class ServiceBusQueueBrokerOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the enqueue behaviors that wrap enqueue operations.
    /// </summary>
    public IEnumerable<IQueueEnqueuerBehavior> EnqueuerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler behaviors that wrap queue handler execution.
    /// </summary>
    public IEnumerable<IQueueHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the factory used to resolve queue handlers.
    /// </summary>
    public IQueueMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the serializer used for queue message payloads.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets the Azure Service Bus connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the optional prefix applied to queue names derived from message types.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the optional suffix applied to queue names derived from message types.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages that the processor will concurrently process.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 8;

    /// <summary>
    /// Gets or sets the number of messages to request from the service during each receive operation.
    /// </summary>
    public int PrefetchCount { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether the broker should create queues at runtime.
    /// </summary>
    public bool AutoCreateQueue { get; set; } = true;

    /// <summary>
    /// Gets or sets the default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before a message is dead-lettered.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the delay in milliseconds applied before processing a received message.
    /// </summary>
    public int ProcessDelay { get; set; } = 100;
}
