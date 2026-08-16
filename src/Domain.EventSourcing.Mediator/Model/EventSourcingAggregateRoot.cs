// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Model;

using Common;
using Domain.Model;
using MediatR;
using Newtonsoft.Json;

// TODO: get rid of Newtonsoft dependency

/// <summary>
/// Represents event sourcing aggregate root.
/// </summary>
public abstract class EventSourcingAggregateRoot : AggregateRoot<Guid>, IAggregateRootWithGuid, IAggregateRootCommitting
{
    private readonly IList<IAggregateEvent> unsavedEvents = [];

    /// <summary>
    /// Initializes a new instance of the <c>EventSourcingAggregateRoot</c> class.
    /// </summary>
    /// <param name="event">The event used by the operation.</param>
    [JsonConstructor] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    protected EventSourcingAggregateRoot(IAggregateEvent @event)
    {
        if (@event is not null)
        {
            this.Id = @event.AggregateId;
            this.ReceiveEvent(@event);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <c>EventSourcingAggregateRoot</c> class.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="events">The events used by the operation.</param>
    protected EventSourcingAggregateRoot(Guid id, IEnumerable<IAggregateEvent> events)
    {
        this.Id = id;

        if (events is not null)
        {
            foreach (var aggregateEvent in events.OrderBy(x => x.AggregateVersion))
            {
                this.IntegrateEvent(aggregateEvent);
            }
        }
    }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Gets the unsaved events.
    /// </summary>
    public IEnumerable<IAggregateEvent> UnsavedEvents => this.unsavedEvents;

    /// <summary>
    /// Executes the event has been added to event store operation.
    /// </summary>
    /// <param name="event">The event used by the operation.</param>
    public void EventHasBeenAddedToEventStore(IAggregateEvent @event)
    {
        this.unsavedEvents.Remove(@event);
    }

    async Task IAggregateRootCommitting.EventHasBeenCommittedAsync(IMediator mediator, IAggregateEvent @event)
    {
        await mediator.Publish(@event, CancellationToken.None).AnyContext();
    }

    /// <summary>
    /// Executes the event has been commited operation.
    /// </summary>
    /// <param name="mediator">The mediator used by the operation.</param>
    /// <param name="event">The event used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task EventHasBeenCommitedAsync(IMediator mediator, IAggregateEvent @event)
    {
        await (this as IAggregateRootCommitting).EventHasBeenCommittedAsync(mediator, @event).AnyContext();
    }

    /// <summary>
    /// Executes the apply event operation.
    /// </summary>
    /// <param name="event">The event used by the operation.</param>
    protected void ApplyEvent(IAggregateEvent @event)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.AsReflectionDynamic().Apply(@event);
    }

    /// <summary>
    /// Executes the receive event operation.
    /// </summary>
    /// <param name="event">The event used by the operation.</param>
    protected void ReceiveEvent(IAggregateEvent @event)
    {
        this.IntegrateEvent(@event);
        this.unsavedEvents.Add(@event);
    }

    /// <summary>
    /// Gets next version.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    protected int GetNextVersion()
    {
        return this.Version + 1;
    }

    private void IntegrateEvent(IAggregateEvent @event)
    {
        if (!@event.AggregateId.Equals(this.Id))
        {
            throw new InvalidOperationException(
                $"Cannot integrate event with {nameof(@event.AggregateId)} '{@event.AggregateId}' on an aggregate with {nameof(this.Id)} '{this.Id}'");
        }

        if (@event.AggregateVersion != this.GetNextVersion())
        {
            throw new InvalidOperationException(
                $"Cannot integrate event with {nameof(@event.AggregateVersion)} '{@event.AggregateVersion}' on an aggregate with {nameof(this.Version)} '{this.Version}'");
        }

        this.ApplyEvent(@event);
        this.Version = @event.AggregateVersion;
    }
}
