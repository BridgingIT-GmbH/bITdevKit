// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.Logging;

/// <summary>Configures durable queueing without changing the existing handler pipeline.</summary>
public sealed class LiteDbQueueBrokerOptions : LiteDbFeatureOptions
{
    /// <summary>Gets or sets the publisher behaviors.</summary>
    public IEnumerable<IQueueEnqueuerBehavior> EnqueuerBehaviors { get; set; } = [];

    /// <summary>Gets or sets the handler behaviors.</summary>
    public IEnumerable<IQueueHandlerBehavior> HandlerBehaviors { get; set; } = [];

    /// <summary>Gets or sets the existing scoped handler factory.</summary>
    public IQueueMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>Gets or sets the structured logger factory.</summary>
    public ILoggerFactory LoggerFactory { get; set; }

    /// <summary>Gets or sets whether background delivery is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the initial background delay.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets or sets the durable polling interval and failed-delivery backoff.</summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the maximum events claimed per invocation.</summary>
    public int BatchSize { get; set; } = 128;

    /// <summary>Gets or sets the delivery attempt limit for each handler.</summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>Gets or sets the processing lease duration.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the lease renewal interval.</summary>
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets optional message expiration from creation.</summary>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromHours(1);
    /// <summary>Gets or sets the queue-name prefix.</summary>
    public string QueueNamePrefix { get; set; }

    /// <summary>Gets or sets the queue-name suffix.</summary>
    public string QueueNameSuffix { get; set; }
}
