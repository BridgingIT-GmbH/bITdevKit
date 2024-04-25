// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

public interface IEventStore<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    Task SaveEventsAsync(TAggregate aggregate, CancellationToken cancellationToken);

    Task SaveEventsAsync(TAggregate aggregate, bool sendProjectionRequestForEveryEvent,
        CancellationToken cancellationToken);

    Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken);

    Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId);

    Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken);

    Task<TAggregate> GetAsync(Guid aggregateId, bool forceReplay, CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetAggregateIdsAsync(CancellationToken cancellationToken);
}