// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing;

using Common;
using Newtonsoft.Json; // TODO: get rid of Newtonsoft dependency

/// <summary>
/// Represents the domain event domain event.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public class DomainEvent<TId> : IDomainEvent<TId>
{
    /// <summary>
    /// Initializes a new instance of the <c>DomainEvent</c> class.
    /// </summary>
    public DomainEvent()
    {
        this.EventId = GuidGenerator.CreateSequential();
        this.Timestamp = DateTime.UtcNow;
        this.AggregateId = default;
    }

    /// <summary>
    /// Initializes a new instance of the <c>DomainEvent</c> class.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    public DomainEvent(TId aggregateId)
    {
        this.EventId = GuidGenerator.CreateSequential();
        this.Timestamp = DateTime.UtcNow;
        this.NotificationId = this.EventId;
        this.NotificationTimestamp = this.Timestamp;
        this.AggregateId = aggregateId;
    }

    /// <summary>
    /// Gets or sets the event id.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public Guid EventId { get; private set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Gets or sets the aggregate id.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public TId AggregateId { get; private set; }

    /// <summary>
    /// Gets or sets the notification id.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public Guid NotificationId { get; private set; }

    /// <summary>
    /// Gets or sets the notification timestamp.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public DateTimeOffset NotificationTimestamp { get; private set; }

    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    public virtual IDictionary<string, object> Properties { get; protected set; } = new Dictionary<string, object>();
}
