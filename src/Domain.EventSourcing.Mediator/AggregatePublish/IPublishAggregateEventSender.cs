// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using Model;

public interface IPublishAggregateEventSender
{
    /// <summary>
    ///     Veröffentlicht das Domänenevent <see cref="currentEvent" /> für das Aggregat <see cref="aggregate" /> in die
    ///     Outbox. Die Veröffentlichung wird
    ///     innerhalb einer Transaktion beim Speichern im EventStore ausgelöst.
    /// </summary>
    Task WriteToOutboxAsync<TAggregate>(AggregateEvent currentEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als ProjectionRequest für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorRequestSender" />.
    ///     Die Veröffentlichung erfolgt nur, falls <see cref="EventStorePublishingModes.SendProjectionRequestUsingMediator" />
    ///     gesetzt ist./>
    /// </summary>
    Task SendProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als ProjectionRequest in Form einer Notification für das
    ///     Aggregat <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorNotificationSender" />
    ///     Die Veröffentlichung erfolgt nur, falls <see cref="EventStorePublishingModes.NotifyForProjectionUsingMediator" />
    ///     gesetzt ist.
    /// </summary>
    Task PublishProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorRequestSender" />.
    ///     Die Veröffentlichung erfolgt nur, falls
    ///     <see cref="EventStorePublishingModes.SendEventOccuredRequestUsingMediator" /> gesetzt ist
    /// </summary>
    Task SendEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorNotificationSender" />
    ///     als Notification.
    ///     Die Veröffentlichung erfolgt nur, falls
    ///     <see cref="EventStorePublishingModes.NotifiyEventOccuredRequestUsingMediator" /> gesetzt ist.
    /// </summary>
    Task PublishEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung von <see cref="aggregateEventMediatorNotificationSender" />
    ///     als Notification.
    ///     Die Veröffentlichung erfolgt nur, falls
    ///     <see cref="EventStorePublishingModes.NotifiyEventOccuredRequestUsingMediator" /> gesetzt ist.
    /// </summary>
    [Obsolete("Please use PublishEventOccuredAsync")]
    Task NotifyEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;
}