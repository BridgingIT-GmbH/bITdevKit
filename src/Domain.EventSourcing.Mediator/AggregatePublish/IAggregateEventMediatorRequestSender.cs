// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.AggregatePublish;

using Model;

/// <summary>
/// Defines operations for i aggregate event mediator request sender.
/// </summary>
public interface IAggregateEventMediatorRequestSender
{
    /// <summary>
    /// Executes the send projection event operation.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendProjectionEventAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Executes the send projection event operation.
    /// </summary>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> SendProjectionEventAsync(object savedEvent, object aggregate);

    /// <summary>
    /// Executes the send event occured operation.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendEventOccuredAsync<TAggregate>(IAggregateEvent savedEvent, TAggregate aggregate)
        where TAggregate : EventSourcingAggregateRoot;

    /// <summary>
    /// Executes the send event occured operation.
    /// </summary>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <param name="aggregate">The aggregate used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<bool> SendEventOccuredAsync(object savedEvent, object aggregate);
}
