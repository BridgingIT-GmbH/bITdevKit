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

public abstract class EventSourcingAggregateRoot : AggregateRoot<Guid>, IAggregateRootWithGuid, IAggregateRootCommitting
{
    private readonly IList<IAggregateEvent> unsavedEvents = new List<IAggregateEvent>();

    [JsonConstructor] // TODO: refactor this (ContractResolver?) so the JsonNet dependency is not needed (less JsonNet dependencies)
    protected EventSourcingAggregateRoot(IAggregateEvent @event)
    {
        if (@event is not null)
        {
            this.Id = @event.AggregateId;
            this.ReceiveEvent(@event);
        }
    }

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

    public int Version { get; private set; }

    public IEnumerable<IAggregateEvent> UnsavedEvents => this.unsavedEvents;

    public void EventHasBeenAddedToEventStore(IAggregateEvent @event)
    {
        this.unsavedEvents.Remove(@event);
    }

    async Task IAggregateRootCommitting.EventHasBeenCommittedAsync(IMediator mediator, IAggregateEvent @event)
    {
        await mediator.Publish(@event, CancellationToken.None).AnyContext();
    }

    protected async Task EventHasBeenCommitedAsync(IMediator mediator, IAggregateEvent @event)
    {
        await (this as IAggregateRootCommitting).EventHasBeenCommittedAsync(mediator, @event).AnyContext();
    }

    protected void ApplyEvent(IAggregateEvent @event)
    {
        EnsureArg.IsNotNull(@event, nameof(@event));

        this.AsReflectionDynamic().Apply(@event);
    }

    protected void ReceiveEvent(IAggregateEvent @event)
    {
        this.IntegrateEvent(@event);
        this.unsavedEvents.Add(@event);
    }

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