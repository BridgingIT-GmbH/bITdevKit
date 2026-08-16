// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

/// <summary>
/// Represents event store memory data.
/// </summary>
/// <param name="aggregateId">The aggregate id used by the operation.</param>
/// <param name="aggregateType">The aggregate type used by the operation.</param>
public class EventStoreMemoryData(Guid aggregateId, string aggregateType)
{
    /// <summary>
    /// Gets or sets the aggregate id.
    /// </summary>
    public Guid AggregateId { get; private set; } = aggregateId;

    /// <summary>
    /// Gets or sets the aggregate type.
    /// </summary>
    public string AggregateType { get; private set; } = aggregateType;

    /// <summary>
    /// Gets or sets the event blobs.
    /// </summary>
    public List<EventBlob> EventBlobs { get; private set; } = [];
}
