// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

/// <summary>
/// Configuration values for binding the Azure Queue Storage broker from <see cref="Microsoft.Extensions.Configuration.IConfiguration" />.
/// </summary>
public class AzureQueueStorageQueueBrokerConfiguration
{
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
    /// Gets or sets a value indicating whether queues should be created automatically.
    /// </summary>
    public bool? AutoCreateQueue { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent processing calls.
    /// </summary>
    public int? MaxConcurrentCalls { get; set; }

    /// <summary>
    /// Gets or sets the visibility timeout for dequeued messages.
    /// </summary>
    public TimeSpan? VisibilityTimeout { get; set; }

    /// <summary>
    /// Gets or sets the polling interval when no messages are available.
    /// </summary>
    public TimeSpan? PollingInterval { get; set; }

    /// <summary>
    /// Gets or sets the retry delay before a failed message becomes visible again.
    /// </summary>
    public TimeSpan? RetryDelay { get; set; }

    /// <summary>
    /// Gets or sets the default message time to live.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before dead-lettering.
    /// </summary>
    public int? MaxDeliveryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the processing delay in milliseconds.
    /// </summary>
    public int? ProcessDelay { get; set; }
}
