// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.Model;

/// <summary>
/// Represents event store snapshot.
/// </summary>
public class EventStoreSnapshot : AggregateRoot<Guid>
{
    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// Gets or sets the aggregate type.
    /// </summary>
    public string AggregateType { get; set; }

    /// <summary>
    /// Gets or sets the snapshot date.
    /// </summary>
    public DateTime SnapshotDate { get; set; }
}
