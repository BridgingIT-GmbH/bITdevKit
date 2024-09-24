// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using Common;
using Domain.EventSourcing.AggregatePublish;
using Domain.EventSourcing.Model;
using MediatR;

public class PublishAggregateEventSender(
    IMediator mediator,
    EventStorePublishingModes eventStorePublishingModes,
    IAggregateEventMediatorRequestSender aggregateEventMediatorRequestSender,
    IAggregateEventMediatorNotificationSender aggregateEventMediatorNotificationSender,
    IAggregateEventOutboxSender aggregateEventOutboxSender) : IPublishAggregateEventSender
{
    private readonly IMediator mediator = mediator;

    private readonly IAggregateEventMediatorRequestSender aggregateEventMediatorRequestSender =
        aggregateEventMediatorRequestSender;

    private readonly IAggregateEventMediatorNotificationSender aggregateEventMediatorNotificationSender =
        aggregateEventMediatorNotificationSender;

    private readonly IAggregateEventOutboxSender aggregateEventOutboxSender = aggregateEventOutboxSender;
    private readonly EventStorePublishingModes eventStorePublishingModes = eventStorePublishingModes;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="currentEvent" /> für das Aggregat <see cref="aggregate" /> unter Nutzung
    ///     von <see cref="aggregateEventOutboxSender" />.
    ///     Wenn in <see cref="eventStorePublishingModes" /> das Flag <see cref="EventStorePublishingModes.AddToOutbox" />
    ///     gesetzt ist,
    ///     wird in die Outbox geschrieben. Ansonsten passiert nichts />
    /// </summary>
    public async Task WriteToOutboxAsync<TAggregate>(AggregateEvent currentEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        if ((this.eventStorePublishingModes & EventStorePublishingModes.AddToOutbox) != EventStorePublishingModes.None)
        {
            await this.aggregateEventOutboxSender.WriteToOutboxAsync(currentEvent, aggregate).AnyContext();
        }
    }

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als ProjectionRequest für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorRequestSender" />.
    ///     Die Veröffentlichung erfolgt nur, falls in  <see cref="eventStorePublishingModes" />
    ///     <see cref="EventStorePublishingModes.SendProjectionRequestUsingMediator" /> gesetzt ist./>
    /// </summary>
    public async Task SendProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        if ((this.eventStorePublishingModes & EventStorePublishingModes.SendProjectionRequestUsingMediator) !=
            EventStorePublishingModes.None)
        {
            await this.aggregateEventMediatorRequestSender.SendProjectionEventAsync(savedEvent, aggregate).AnyContext();
        }
    }

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorRequestSender" />.
    ///     Die Veröffentlichung erfolgt nur, falls in  <see cref="eventStorePublishingModes" />
    ///     <see cref="EventStorePublishingModes.SendEventOccuredRequestUsingMediator" /> gesetzt ist
    /// </summary>
    public async Task SendEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        if ((this.eventStorePublishingModes & EventStorePublishingModes.SendEventOccuredRequestUsingMediator) !=
            EventStorePublishingModes.None)
        {
            await this.aggregateEventMediatorRequestSender.SendEventOccuredAsync(savedEvent, aggregate).AnyContext();
        }
    }

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als ProjectionRequest in Form einer Notification für das
    ///     Aggregat <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorNotificationSender" />
    ///     Die Veröffentlichung erfolgt nur, falls in  <see cref="eventStorePublishingModes" />
    ///     <see cref="EventStorePublishingModes.NotifyForProjectionUsingMediator" /> gesetzt ist.
    /// </summary>
    public async Task PublishProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        if ((this.eventStorePublishingModes & EventStorePublishingModes.NotifyForProjectionUsingMediator) !=
            EventStorePublishingModes.None)
        {
            await this.aggregateEventMediatorNotificationSender.PublishProjectionEventAsync(savedEvent, aggregate)
                .AnyContext();
        }
    }

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorNotificationSender" />
    ///     als Notification.
    ///     Die Veröffentlichung erfolgt nur, falls in  <see cref="eventStorePublishingModes" />
    ///     <see cref="EventStorePublishingModes.NotifyEventOccuredUsingMediator" /> gesetzt ist.
    /// </summary>
    public async Task PublishEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        if ((this.eventStorePublishingModes & EventStorePublishingModes.NotifyEventOccuredUsingMediator) !=
            EventStorePublishingModes.None)
        {
            await this.aggregateEventMediatorNotificationSender.PublishEventOccuredAsync(savedEvent, aggregate)
                .AnyContext();
        }
    }

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorNotificationSender" />
    ///     als Notification.
    ///     Die Veröffentlichung erfolgt nur, falls in  <see cref="eventStorePublishingModes" />
    ///     <see cref="EventStorePublishingModes.NotifyEventOccuredUsingMediator" /> gesetzt ist.
    /// </summary>
    [Obsolete("Please use PublishEventOccuredAsync")]
    public async Task NotifyEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot
    {
        await this.PublishEventOccuredAsync(savedEvent, aggregate).AnyContext();
    }
}