// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Common;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Registration;
using Domain.EventSourcing.Store;

/// <summary>
///     EventStore, der die Events über EntityFramework in einer SqlServer-Datenbank persistiert.
///     Der EventStore setzt voraus, dass alle zu persistierenden Aggregates \ AggregateEvents
///     registriert wurden, so dass ein "immutable Name" intern werden kann.
///     Dies ermöglicht, dass Aggregates bzw. AggregateEvents verschoben werden können bzw.
///     umbenannt werden können, solange sich der "immutable Name" nicht ändert.
///     <see cref="IEventStoreAggregateRegistration" /> bzw.
///     <see cref="IEventStoreAggregateEventRegistration" />.
/// </summary>
public class EventStoreRepository : IEventStoreRepository
{
    private readonly ISerializer serializer;
    private readonly IEventStoreAggregateEventRegistration aggregateEventRegistration;
    private readonly IEventStoreAggregateRegistration aggregateRegistration;
    private readonly IAggregateEventRepository aggregateEventRepository;
    private readonly ISnapshotRepository snapshotRepository;
    private readonly IEventTypeSelector typeSelector;
    private readonly IAggregateTypeSelector aggregateTypeSelector;

    public EventStoreRepository(
        ISerializer serializer,
        IAggregateEventRepository aggregateEventRepository,
        ISnapshotRepository snapshotRepository,
        IEventStoreAggregateRegistration aggregateRegistration,
        IEventStoreAggregateEventRegistration aggregateEventRegistration,
        IEventTypeSelector typeSelector,
        IAggregateTypeSelector aggregateTypeSelector)
    {
        EnsureArg.IsNotNull(aggregateEventRepository, nameof(aggregateEventRepository));
        EnsureArg.IsNotNull(serializer, nameof(serializer));
        EnsureArg.IsNotNull(typeSelector, nameof(typeSelector));
        EnsureArg.IsNotNull(aggregateTypeSelector, nameof(aggregateTypeSelector));

        this.aggregateEventRepository = aggregateEventRepository;
        this.snapshotRepository = snapshotRepository;
        this.serializer = serializer;
        this.typeSelector = typeSelector;
        this.aggregateTypeSelector = aggregateTypeSelector;
        this.aggregateRegistration = aggregateRegistration;
        this.aggregateEventRegistration = aggregateEventRegistration;
    }

    public async Task AddAsync<TAggregate>(IAggregateEvent @event, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        await this.aggregateEventRepository.InsertAsync(@event,
                this.aggregateRegistration.GetImmutableName<TAggregate>(),
                this.aggregateEventRegistration.GetImmutableName(@event),
                this.serializer.SerializeToBytes(@event))
            .AnyContext();
    }

    public async Task<IAggregateEvent[]> GetEventsAsync<TAggregate>(
        Guid aggregateId,
        CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        var immutableAggregateName = this.aggregateRegistration.GetImmutableName<TAggregate>();
        var events = await this.aggregateEventRepository
            .GetEventsAsync(aggregateId, immutableAggregateName, cancellationToken)
            .AnyContext();

        return events?.Select(ev =>
                ev.Data.ConvertFromBlob(this.aggregateEventRegistration.GetTypeOnImmutableName(ev.EventType),
                    this.serializer,
                    this.typeSelector))
            .ToArray();
    }

    public async Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        return await this.aggregateEventRepository.GetAggregateIdsAsync(cancellationToken).AnyContext();
    }

    public async Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        return await this.aggregateEventRepository.GetAggregateIdsAsync<TAggregate>(cancellationToken).AnyContext();
    }

    public async Task ExecuteScopedAsync(Func<Task> operation)
    {
        EnsureArg.IsNotNull(operation, nameof(operation));
        await this.aggregateEventRepository.ExecuteScopedAsync(operation).AnyContext();
    }

    public async Task<int> GetMaxVersionAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        var immutableAggregateName = this.aggregateRegistration.GetImmutableName<TAggregate>();
        return await this.aggregateEventRepository
            .GetMaxVersionAsync(aggregateId, immutableAggregateName, cancellationToken)
            .AnyContext();
    }

    public async Task<TAggregate?> GetSnapshotAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        var immutableAggregateName = this.aggregateRegistration.GetImmutableName<TAggregate>();
        var aggregateBlob = await this.snapshotRepository
            .GetSnapshotAsync(aggregateId, immutableAggregateName, cancellationToken)
            .AnyContext();
        if (aggregateBlob is not null)
        {
            var aggregate = aggregateBlob.ConvertFromBlob(
                typeof(TAggregate).FullName ?? throw new InvalidOperationException(),
                this.serializer,
                this.aggregateTypeSelector);
            return aggregate as TAggregate;
        }

        return null;
    }

    public async Task SaveSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        var immutableAggregateName = this.aggregateRegistration.GetImmutableName<TAggregate>();
        var data = aggregate.ConvertToBlob(this.serializer);
        await this.snapshotRepository
            .SaveSnapshotAsync(aggregate.Id, data.Blob, immutableAggregateName, cancellationToken)
            .AnyContext();
    }
}