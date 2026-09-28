// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing;

using System.Diagnostics;
using Common;
using Domain.EventSourcing.AggregatePublish;
using Domain.EventSourcing.Model;
using Domain.EventSourcing.Repositories;
using Domain.EventSourcing.Store;
using MediatR;

/// <summary>
/// Represents event store.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <param name="mediator">The mediator used by the operation.</param>
/// <param name="eventStoreRepository">The event store repository used by the operation.</param>
/// <param name="eventSender">The event sender used by the operation.</param>
/// <param name="eventStoreOptions">The event store options used by the operation.</param>
public class EventStore<TAggregate>(
    IMediator mediator,
    IEventStoreRepository eventStoreRepository,
    IPublishAggregateEventSender eventSender,
    IEventStoreOptions<TAggregate> eventStoreOptions) : IEventStore<TAggregate>
    where TAggregate : EventSourcingAggregateRoot
{
    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        return await this.GetAsync(aggregateId, false, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Gets .
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="forceReplay">The force replay used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TAggregate> GetAsync(Guid aggregateId, bool forceReplay, CancellationToken cancellationToken)
    {
        var snapshot = eventStoreOptions.IsSnapshotEnabled
            ? await eventStoreRepository.GetSnapshotAsync<TAggregate>(aggregateId, CancellationToken.None)
                .AnyContext()
            : null;
        if (snapshot is null || forceReplay)
        {
            var events = await this.GetEventsAsync(aggregateId, cancellationToken).AnyContext();
            if (events is null || events.Length == 0)
            {
                return null;
            }

            var methodInfo = typeof(TAggregate).GetConstructor([typeof(Guid), typeof(IEnumerable<IAggregateEvent>)]);
            if (methodInfo is null)
            {
                throw new AggregateCouldNotBeConstructedException();
            }

            var aggregate = methodInfo.Invoke([aggregateId, events]) as TAggregate;
            if (aggregate is null)
            {
                throw new AggregateException(
                    $"Aggregate {typeof(TAggregate)} with id {aggregateId} could not be created");
            }

            if (eventStoreOptions.IsSnapshotEnabled)
            {
                await eventStoreRepository.SaveSnapshotAsync(aggregate, cancellationToken).AnyContext();
            }

            return aggregate;
        }

        return snapshot;
    }

    /// <summary>
    /// Gets aggregate ids.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<Guid>> GetAggregateIdsAsync(CancellationToken cancellationToken)
    {
        return await eventStoreRepository.GetAggregateIdsAsync<TAggregate>(cancellationToken).AnyContext();
    }

    /// <summary>
    /// Saves events.
    /// </summary>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveEventsAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(aggregate, nameof(aggregate));

        await this.SaveEventsAsync(aggregate, true, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Saves events.
    /// </summary>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <param name="sendProjectionRequestForEveryEvent">The send projection request for every event used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveEventsAsync(
        TAggregate aggregate,
        bool sendProjectionRequestForEveryEvent,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(aggregate, nameof(aggregate));

        var maxVersion = await eventStoreRepository.GetMaxVersionAsync<TAggregate>(aggregate.Id, cancellationToken)
            .AnyContext();

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

                await eventStoreRepository.AddAsync<TAggregate>(@event, cancellationToken).AnyContext();
                (aggregate as IAggregateRootCommitting).EventHasBeenAddedToEventStore(@event);
                await eventSender.WriteToOutboxAsync(@event as AggregateEvent, aggregate).AnyContext();
                try
                {
                    await (aggregate as IAggregateRootCommitting).EventHasBeenCommittedAsync(mediator, @event)
                        .AnyContext();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);

                    throw;
                }

                if (sendProjectionRequestForEveryEvent)
                {
                    await eventSender.SendProjectionEventAsync(@event, aggregate).AnyContext();
                }

                await eventSender.SendEventOccuredAsync(@event, aggregate).AnyContext();

                await eventSender.PublishProjectionEventAsync(@event, aggregate).AnyContext();
                await eventSender.PublishEventOccuredAsync(@event, aggregate).AnyContext();
            }

            if (eventStoreOptions.IsSnapshotEnabled)
            {
                await eventStoreRepository.SaveSnapshotAsync(aggregate, cancellationToken).AnyContext();
            }
        });

        await eventStoreRepository.ExecuteScopedAsync(fn).AnyContext();

        if (!sendProjectionRequestForEveryEvent)
        {
            await eventSender.SendProjectionEventAsync(null, aggregate).AnyContext();
        }
    }

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        return await eventStoreRepository.GetEventsAsync<TAggregate>(aggregateId, CancellationToken.None)
            .AnyContext();
    }

    /// <summary>
    /// Gets events.
    /// </summary>
    /// <param name="aggregateId">The aggregate id used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IAggregateEvent[]> GetEventsAsync(Guid aggregateId)
    {
        return await this.GetEventsAsync(aggregateId, CancellationToken.None).AnyContext();
    }
}
