// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.Model;

/// <summary>
/// Represents the event store aggregate event domain event.
/// </summary>
public class EventStoreAggregateEvent : AggregateRoot<Guid>
{
    /// <summary>
    /// Gets or sets the aggregate id.
    /// </summary>
    public Guid AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the aggregate version.
    /// </summary>
    public int AggregateVersion { get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Identifier { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// Gets or sets the time stamp.
    /// </summary>
    public DateTime TimeStamp { get; set; }

    /// <summary>
    /// Gets or sets the aggregate type.
    /// </summary>
    public string AggregateType { get; set; }
}
