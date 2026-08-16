// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.EventSourcing.Model;

/// <summary>
/// Defines operations for i event store options.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
public interface IEventStoreOptions<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    /// <summary>
    /// Gets or sets the is snapshot enabled.
    /// </summary>
    bool IsSnapshotEnabled { get; init; }
}
