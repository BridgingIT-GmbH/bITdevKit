// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;

/// <summary>
/// Options for configuring the <see cref="AzureQueueStorageQueueBroker" />.
/// </summary>
public class AzureQueueStorageQueueBrokerOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the enqueuer behaviors.
    /// </summary>
    public IEnumerable<IQueueEnqueuerBehavior> EnqueuerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler behaviors.
    /// </summary>
    public IEnumerable<IQueueHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler factory.
    /// </summary>
    public IQueueMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the serializer.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets the Azure Queue Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the queue name prefix.
    /// </summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the queue name suffix.
    /// </summary>
    public string QueueNameSuffix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether queues should be created automatically at runtime.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool AutoCreateQueue { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of messages to receive and process concurrently per queue.
    /// </summary>
    /// <remarks>Defaults to 1. Maximum value is 32.</remarks>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets the visibility timeout for dequeued messages.
    /// </summary>
    /// <remarks>
    /// Messages become invisible for this duration after being received.
    /// If not deleted within this window, they become visible again for retry.
    /// Defaults to 30 seconds.
    /// </remarks>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the delay between polling operations when no messages are available.
    /// </summary>
    /// <remarks>Defaults to 1 second.</remarks>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the delay applied before a retried message becomes visible again.
    /// </summary>
    /// <remarks>Defaults to 2 seconds.</remarks>
    public TimeSpan? RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the default message time to live.
    /// </summary>
    /// <remarks>Defaults to 7 days.</remarks>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before a message is dead-lettered.
    /// </summary>
    /// <remarks>Defaults to 5.</remarks>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the processing delay in milliseconds applied before invoking the handler.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public int ProcessDelay { get; set; } = 0;
}
