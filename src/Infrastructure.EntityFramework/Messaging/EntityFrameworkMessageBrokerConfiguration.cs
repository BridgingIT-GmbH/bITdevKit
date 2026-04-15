// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Provides configuration-bound settings for the Entity Framework backed message broker.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddMessaging(builder.Configuration)
///     .WithEntityFrameworkBroker&lt;AppDbContext&gt;(
///         new EntityFrameworkMessageBrokerConfiguration
///         {
///             ProcessingInterval = TimeSpan.FromSeconds(10),
///             LeaseDuration = TimeSpan.FromSeconds(30),
///             MaxDeliveryAttempts = 5,
///             MessageExpiration = TimeSpan.FromHours(1)
///         });
/// </code>
/// </example>
public class EntityFrameworkMessageBrokerConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the broker runtime is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay applied after application startup before broker processing begins.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the interval between worker polling cycles.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets an optional delay applied before each processing cycle.
    /// </summary>
    public TimeSpan ProcessingDelay { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages processed in a single worker cycle.
    /// </summary>
    public int ProcessingCount { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts allowed per handler entry.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration of a worker lease on a broker message.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the interval used to renew active leases.
    /// </summary>
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the default expiration window applied to published messages.
    /// </summary>
    public TimeSpan? MessageExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the age after which terminal messages may be archived automatically.
    /// </summary>
    public TimeSpan? AutoArchiveAfter { get; set; }

    /// <summary>
    /// Gets or sets the aggregate statuses eligible for automatic archiving.
    /// </summary>
    public IEnumerable<BrokerMessageStatus> AutoArchiveStatuses { get; set; } =
    [
        BrokerMessageStatus.Succeeded,
        BrokerMessageStatus.DeadLettered,
        BrokerMessageStatus.Expired
    ];

    /// <summary>
    /// Gets or sets a value indicating whether published messages are saved immediately.
    /// </summary>
    public bool AutoSave { get; set; } = true;
}