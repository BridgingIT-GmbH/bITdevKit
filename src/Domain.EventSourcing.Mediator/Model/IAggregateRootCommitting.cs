// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

using MediatR;

/// <summary>
/// Defines operations for i aggregate root committing.
/// </summary>
public interface IAggregateRootCommitting
{
    /// <summary>
    /// Executes the event has been added to event store operation.
    /// </summary>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    void EventHasBeenAddedToEventStore(IAggregateEvent savedEvent);

    /// <summary>
    /// Executes the event has been committed operation.
    /// </summary>
    /// <param name="mediator">The mediator used by the operation.</param>
    /// <param name="savedEvent">The saved event used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task EventHasBeenCommittedAsync(IMediator mediator, IAggregateEvent savedEvent);
}
