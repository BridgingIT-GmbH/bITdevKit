// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System;
using System.Threading;
using System.Threading.Tasks;

using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.Repositories;

public interface IAggregateEventRepository : IRepository
{
    Task InsertAsync(IAggregateEvent @event, string immutableAggregateTypeName, string immutableEventTypeName, byte[] data);

    Task<EventStoreAggregateEvent[]> GetEventsAsync(Guid aggregateId, string immutableAggregateTypeName, CancellationToken cancellationToken);

    Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken);

    Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    Task ExecuteScopedAsync(Func<Task> operation);

    Task<int> GetMaxVersionAsync(Guid aggregateId, string immutableAggregateName, CancellationToken cancellationToken);
}