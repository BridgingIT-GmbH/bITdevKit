// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;

/// <summary>
/// Options for configuring the <see cref="AzureQueueStorageMessageBroker" />.
/// </summary>
public class AzureQueueStorageMessageBrokerOptions : OptionsBase
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
    public bool AutoCreateQueue { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of messages to receive and process concurrently per queue.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets the visibility timeout for dequeued messages.
    /// </summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the delay between polling operations when no messages are available.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the processing delay in milliseconds applied before invoking the handler.
    /// </summary>
    public int ProcessDelay { get; set; } = 0;
}
