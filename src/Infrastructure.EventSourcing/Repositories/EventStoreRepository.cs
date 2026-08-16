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

    /// <summary>
    /// Initializes a new instance of the <c>EventStoreRepository</c> class.
    /// </summary>
    /// <param name="serializer">The serializer used by the operation.</param>
    /// <param name="aggregateEventRepository">The aggregate event repository used by the operation.</param>
    /// <param name="snapshotRepository">The snapshot repository used by the operation.</param>
    /// <param name="aggregateRegistration">The aggregate registration used by the operation.</param>
    /// <param name="aggregateEventRegistration">The aggregate event registration used by the operation.</param>
    /// <param name="typeSelector">The type selector used by the operation.</param>
    /// <param name="aggregateTypeSelector">The aggregate type selector used by the operation.</param>
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

    /// <summary>
    /// Adds .
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="event">The event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        return await this.aggregateEventRepository.GetAggregateIdsAsync(cancellationToken).AnyContext();
    }

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        return await this.aggregateEventRepository.GetAggregateIdsAsync<TAggregate>(cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="operation">The operation used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteScopedAsync(Func<Task> operation)
    {
        EnsureArg.IsNotNull(operation, nameof(operation));
        await this.aggregateEventRepository.ExecuteScopedAsync(operation).AnyContext();
    }

    /// <summary>
    /// Gets max version.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<int> GetMaxVersionAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        var immutableAggregateName = this.aggregateRegistration.GetImmutableName<TAggregate>();

        return await this.aggregateEventRepository
            .GetMaxVersionAsync(aggregateId, immutableAggregateName, cancellationToken)
            .AnyContext();
    }

    /// <summary>
    /// Gets snapshot.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Saves snapshot.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
