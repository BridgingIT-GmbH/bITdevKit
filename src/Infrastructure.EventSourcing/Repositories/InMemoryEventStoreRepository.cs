// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using Common;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Registration;
using Domain.EventSourcing.Store;

public class InMemoryEventStoreRepository : IEventStoreRepository
{
    private readonly Dictionary<Guid, EventStoreMemoryData> events = [];

    private readonly ISerializer serializer;
    private readonly IEventStoreAggregateRegistration aggregateRegistration;

    public InMemoryEventStoreRepository(ISerializer serializer, IEventStoreAggregateRegistration aggregateRegistration)
    {
        EnsureArg.IsNotNull(serializer, nameof(serializer));
        EnsureArg.IsNotNull(aggregateRegistration, nameof(aggregateRegistration));

        this.serializer = serializer;
        this.aggregateRegistration = aggregateRegistration;
    }

    public InMemoryEventStoreRepository()
        : this(new JsonNetSerializer(), new EventStoreAggregateRegistration()) { }

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

    public Task<Guid[]> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() => this.events.Keys.ToArray());
    }

    public Task<Guid[]> GetAggregateIdsAsync<TAggregate>(CancellationToken none)
        where TAggregate : EventSourcingAggregateRoot
    {
        var name = this.aggregateRegistration.GetImmutableName<TAggregate>();

        return Task.Run(() =>
            this.events.Values.Where(v => v.AggregateType == name).Select(ev => ev.AggregateId).Distinct().ToArray());
    }

    public Task SaveAsync()
    {
        return Task.CompletedTask;
    }

    public Task ExecuteScopedAsync(Func<Task> operation)
    {
        operation?.Invoke();

        return Task.CompletedTask;
    }

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

    public Task<TAggregate?> GetSnapshotAsync<TAggregate>(Guid aggregateId, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        return Task.FromResult((TAggregate?)null);
    }

    public Task SaveSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : EventSourcingAggregateRoot
    {
        return Task.CompletedTask;
    }
}