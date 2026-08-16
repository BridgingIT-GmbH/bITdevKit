// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;

using Domain.EventSourcing.Model;

/// <summary>
/// Defines operations for i aggregate event mediator notification sender.
/// </summary>
public interface IAggregateEventMediatorNotificationSender
{
    /// <summary>
    /// Publishes projection event.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PublishProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Publishes event occured.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PublishEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Publishes event occured.
    /// </summary>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> PublishEventOccuredAsync(object savedEvent, object aggregate);
}
