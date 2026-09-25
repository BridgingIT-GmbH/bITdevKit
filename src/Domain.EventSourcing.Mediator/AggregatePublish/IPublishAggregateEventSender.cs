// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using Model;

/// <summary>
/// Defines operations for i publish aggregate event sender.
/// </summary>
public interface IPublishAggregateEventSender
{
    /// <summary>
    ///     Veröffentlicht das Domänenevent <see cref="currentEvent" /> für das Aggregat <see cref="aggregate" /> in die
    ///     Outbox. Die Veröffentlichung wird
    ///     innerhalb einer Transaktion beim Speichern im EventStore ausgelöst.
    /// </summary>
    /// <typeparam name="TAggregate">The event-sourced aggregate type.</typeparam>
    /// <param name="currentEvent">The aggregate event to write.</param>
    /// <param name="aggregate">The aggregate that raised the event.</param>
    /// <returns>A task that completes when the outbox write finishes.</returns>
    Task WriteToOutboxAsync<TAggregate>(AggregateEvent currentEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als ProjectionRequest für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung des konfigurierten Mediator-Request-Senders.
    ///     Die Veröffentlichung erfolgt nur, falls <c>SendProjectionRequestUsingMediator</c> gesetzt ist.
    /// </summary>
    Task SendProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als ProjectionRequest in Form einer Notification für das
    ///     Aggregat <see cref="aggregate" /> unter Nutzung des konfigurierten Mediator-Notification-Senders.
    ///     Die Veröffentlichung erfolgt nur, falls <c>NotifyForProjectionUsingMediator</c>
    ///     gesetzt ist.
    /// </summary>
    Task PublishProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung des konfigurierten Mediator-Request-Senders.
    ///     Die Veröffentlichung erfolgt nur, falls
    ///     <c>SendEventOccuredRequestUsingMediator</c> gesetzt ist.
    /// </summary>
    Task SendEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung des konfigurierten Mediator-Notification-Senders
    ///     als Notification.
    ///     Die Veröffentlichung erfolgt nur, falls
    ///     <c>NotifyEventOccuredUsingMediator</c> gesetzt ist.
    /// </summary>
    Task PublishEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    ///     Veröffentlicht das DomänenEvent <see cref="savedEvent" /> als EventOccured für das Aggregat
    ///     <see cref="aggregate" /> unter Nutzung des konfigurierten Mediator-Notification-Senders
    ///     als Notification.
    ///     Die Veröffentlichung erfolgt nur, falls
    ///     <c>NotifyEventOccuredUsingMediator</c> gesetzt ist.
    /// </summary>
    [Obsolete("Please use PublishEventOccuredAsync")]
    Task NotifyEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;
}
