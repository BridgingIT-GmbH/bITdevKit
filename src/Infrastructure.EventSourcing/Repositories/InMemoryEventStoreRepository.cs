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
/// Represents in memory event store repository.
/// </summary>
public class InMemoryEventStoreRepository : IEventStoreRepository
{
    private readonly Dictionary<Guid, EventStoreMemoryData> events = [];

    private readonly ISerializer serializer;
    private readonly IEventStoreAggregateRegistration aggregateRegistration;

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryEventStoreRepository</c> class.
    /// </summary>
    /// <param name="serializer">The serializer used by the operation.</param>
    /// <param name="aggregateRegistration">The aggregate registration used by the operation.</param>
    public InMemoryEventStoreRepository(ISerializer serializer, IEventStoreAggregateRegistration aggregateRegistration)
    {
        EnsureArg.IsNotNull(serializer, nameof(serializer));
        EnsureArg.IsNotNull(aggregateRegistration, nameof(aggregateRegistration));

        this.serializer = serializer;
        this.aggregateRegistration = aggregateRegistration;
    }

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryEventStoreRepository</c> class.
    /// </summary>
    public InMemoryEventStoreRepository()
        : this(new JsonNetSerializer(), new EventStoreAggregateRegistration()) { }

    /// <summary>
    /// Adds .
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="event">The event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task AddAsync<TAggregate>(IAggregateEvent @event, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        if (!this.events.ContainsKey(@event.AggregateId))
        {
            this.events.Add(@event.AggregateId,
                new EventStoreMemoryData(@event.AggregateId,
                    this.aggregateRegistration.GetImmutableName<TAggregate>()));
        }

        var blob = @event.ConvertToBlob(this.serializer);
        var eventdata = this.events[@event.AggregateId];
        eventdata.EventBlobs.Add(blob);

        return Task.CompletedTask;
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
        var list = this.events[aggregateId].EventBlobs;
        var result = new List<IAggregateEvent>();

        list.ForEach(blob =>
        {
            using var stream = new MemoryStream(blob.Blob);
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var @event = this.serializer.Deserialize(stream, blob.EventType);
            result.Add((IAggregateEvent)@event);
        });

        return await Task.Run(() => result.ToArray()).AnyContext();
    }

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => this.events.Keys.ToArray());
    }

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="none">The none used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken none)
        where TAggregate : EventSourcingAggregateRoot
    {
        var name = this.aggregateRegistration.GetImmutableName<TAggregate>();

        return Task.Run(() =>
            this.events.Values.Where(v => v.AggregateType == name).Select(ev => ev.AggregateId).Distinct().ToArray());
    }

    /// <summary>
    /// Saves .
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SaveAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the execute scoped operation.
    /// </summary>
    /// <param name="operation">The operation used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ExecuteScopedAsync(Func<Task> operation)
    {
        operation?.Invoke();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets max version.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<int> GetMaxVersionAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        if (!this.events.TryGetValue(aggregateId, out var list))
        {
            return Task.FromResult(0);
        }

        var blob = list.EventBlobs.LastOrDefault();
        if (blob is null)
        {
            return Task.FromResult(0);
        }

        using var stream = new MemoryStream(blob.Blob);
        if (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var @event = (IAggregateEvent)this.serializer.Deserialize(stream, blob.EventType);

        return Task.FromResult(@event.AggregateVersion);
    }

    /// <summary>
    /// Gets snapshot.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task<TAggregate?> GetSnapshotAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        return Task.FromResult((TAggregate?)null);
    }

    /// <summary>
    /// Saves snapshot.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SaveSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        return Task.CompletedTask;
    }
}
