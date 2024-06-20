// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;
using BridgingIT.DevKit.Domain.EventSourcing.Model;
using BridgingIT.DevKit.Domain.EventSourcing.Repositories;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using EnsureThat;
using MediatR;

public class EventStore<TAggregate>(
    IMediator mediator,
    IEventStoreRepository eventStoreRepository,
    IPublishAggregateEventSender eventSender,
    IEventStoreOptions<TAggregate> eventStoreOptions) : IEventStore<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    private readonly IMediator mediator = mediator;
    private readonly IEventStoreRepository eventStoreRepository = eventStoreRepository;
    private readonly IPublishAggregateEventSender eventSender = eventSender;
    private readonly IEventStoreOptions<TAggregate> eventStoreOptions = eventStoreOptions;

    public async Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        return await this.GetAsync(aggregateId, false, cancellationToken).AnyContext();
    }

    public async Task<TAggregate> GetAsync(Guid aggregateId, bool forceReplay, CancellationToken cancellationToken)
    {
        var snapshot = this.eventStoreOptions.IsSnapshotEnabled ? await this.eventStoreRepository.GetSnapshotAsync<TAggregate>(aggregateId, CancellationToken.None).AnyContext() : null;
        if (snapshot is null || forceReplay)
        {
            var events = await this.GetEventsAsync(aggregateId, cancellationToken).AnyContext();
            if (events is null || events.Length == 0)
            {
                return null;
            }

            var methodInfo = typeof(TAggregate).GetConstructor(
            [
                typeof(Guid), typeof(IEnumerable<IAggregateEvent>)
            ]);
            if (methodInfo is null)
            {
                throw new AggregateCouldNotBeConstructedException();
            }

            var aggregate = methodInfo.Invoke([aggregateId, events]) as TAggregate;
            if (aggregate is null)
            {
                throw new AggregateException($"Aggregate {typeof(TAggregate)} with id {aggregateId} could not be created");
            }

            if (this.eventStoreOptions.IsSnapshotEnabled)
            {
                await this.eventStoreRepository.SaveSnapshotAsync(aggregate, cancellationToken).AnyContext();
            }

            return aggregate;
        }
        else
        {
            return snapshot;
        }
    }

    public async Task<IEnumerable<Guid>> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        return await this.eventStoreRepository.GetAggregateIdsAsync<TAggregate>(cancellationToken).AnyContext();
    }

    public async Task SaveEventsAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(aggregate, nameof(aggregate));

        await this.SaveEventsAsync(aggregate, true, cancellationToken).AnyContext();
    }

    public async Task SaveEventsAsync(TAggregate aggregate, bool sendProjectionRequestForEveryEvent,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(aggregate, nameof(aggregate));

        var maxVersion = await this.eventStoreRepository
            .GetMaxVersionAsync<TAggregate>(aggregate.Id, cancellationToken).AnyContext();

        var fn = new Func<Task>(async () =>
        {
            var first = true;
            var diff = 0;
            foreach (var @event in aggregate.UnsavedEvents.SafeNull().ToArray())
            {
                if (first)
                {
                    if (@event.AggregateVersion <= maxVersion)
                    {
                        diff = maxVersion - @event.AggregateVersion + 1;
                    }

                    first = false;
                }

                @event.AggregateVersion += diff;

                await this.eventStoreRepository.AddAsync<TAggregate>(@event, cancellationToken).AnyContext();
                (aggregate as IAggregateRootCommitting)?.EventHasBeenAddedToEventStore(@event);
                await this.eventSender.WriteToOutboxAsync(@event as AggregateEvent, aggregate).AnyContext();
                try
                {
                    await (aggregate as IAggregateRootCommitting).EventHasBeenCommittedAsync(this.mediator, @event)
                        .AnyContext();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    throw;
                }

                if (sendProjectionRequestForEveryEvent)
                {
                    await this.eventSender.SendProjectionEventAsync(@event, aggregate).AnyContext();
                }

                await this.eventSender.SendEventOccuredAsync(@event, aggregate).AnyContext();

                await this.eventSender.PublishProjectionEventAsync(@event, aggregate).AnyContext();
                await this.eventSender.PublishEventOccuredAsync(@event, aggregate).AnyContext();
            }

            if (this.eventStoreOptions.IsSnapshotEnabled)
            {
                await this.eventStoreRepository.SaveSnapshotAsync(aggregate, cancellationToken).AnyContext();
            }
        });

        await this.eventStoreRepository.ExecuteScopedAsync(fn).AnyContext();

        if (!sendProjectionRequestForEveryEvent)
        {
            await this.eventSender.SendProjectionEventAsync(null, aggregate).AnyContext();
        }
    }

    public async Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        return await this.eventStoreRepository.GetEventsAsync<TAggregate>(aggregateId, CancellationToken.None).AnyContext();
    }

    public async Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId)
    {
        return await this.GetEventsAsync(aggregateId, CancellationToken.None).AnyContext();
    }
}