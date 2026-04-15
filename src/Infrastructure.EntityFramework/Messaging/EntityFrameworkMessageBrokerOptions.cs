// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents the runtime options used by <see cref="EntityFrameworkMessageBroker{TContext}"/>.
/// </summary>
/// <example>
/// <code>
/// var options = new EntityFrameworkMessageBrokerOptions
/// {
///     Serializer = new SystemTextJsonSerializer(),
///     MaxDeliveryAttempts = 5,
///     LeaseDuration = TimeSpan.FromSeconds(30)
/// };
/// </code>
/// </example>
public class EntityFrameworkMessageBrokerOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the publisher behaviors that wrap publish operations.
    /// </summary>
    public IEnumerable<IMessagePublisherBehavior> PublisherBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the handler behaviors that wrap individual handler executions.
    /// </summary>
    public IEnumerable<IMessageHandlerBehavior> HandlerBehaviors { get; set; }

    /// <summary>
    /// Gets or sets the factory used to resolve message handlers.
    /// </summary>
    public IMessageHandlerFactory HandlerFactory { get; set; }

    /// <summary>
    /// Gets or sets the serializer used for persisted broker payloads.
    /// </summary>
    public ISerializer Serializer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the broker runtime is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether broker messages are saved immediately on publish.
    /// </summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// Gets or sets the startup delay before worker processing begins.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the interval between worker processing cycles.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets an optional delay applied before each worker processing cycle.
    /// </summary>
    public TimeSpan ProcessingDelay { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages processed in a single cycle.
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
    /// Gets or sets the aggregate statuses that qualify for automatic archiving.
    /// </summary>
    public IEnumerable<BrokerMessageStatus> AutoArchiveStatuses { get; set; } =
    [
        BrokerMessageStatus.Succeeded,
        BrokerMessageStatus.DeadLettered,
        BrokerMessageStatus.Expired
    ];
}