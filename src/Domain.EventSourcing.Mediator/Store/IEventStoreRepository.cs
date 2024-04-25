// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

#nullable enable
namespace BridgingIT.DevKit.Domain.EventSourcing.Store;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.EventSourcing.Model;

public interface IEventStoreRepository
{
    Task AddAsync<TAggregate>(IAggregateEvent @event, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    Task<IAggregateEvent[]> GetEventsAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken);

    Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken none)
        where TAggregate : EventSourcingAggregateRoot;

    Task ExecuteScopedAsync(Func<Task> operation);

    Task<int> GetMaxVersionAsync<TAggregate>(Guid eventAggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    Task<TAggregate?> GetSnapshotAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;

    Task SaveSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot;
}