// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Domain.EventSourcing.Model;

public class EventStoreOptions<TAggregate> : IEventStoreOptions<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    public bool IsSnapshotEnabled { get; init; }
}