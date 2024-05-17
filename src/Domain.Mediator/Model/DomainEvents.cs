// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using EnsureThat;
using MediatR;

public class DomainEvents // TODO: create interface?
{
    /// <summary>
    /// The domain registered events.
    /// </summary>
    private ICollection<IDomainEvent> registrations = new List<IDomainEvent>(); // TODO: concurrent collection?

    /// <summary>
    /// Gets all registered domain events.
    /// </summary>
    public IEnumerable<IDomainEvent> GetAll() => this.registrations;

    /// <summary>
    /// Registers the domain events to publish.
    /// Domain Events are only registered on the aggregate root because it is ensuring the integrity of the aggregate as a whole.
    /// </summary>
    /// <param name="events">The events.</param>
    public DomainEvents Register(IEnumerable<IDomainEvent> events, bool ensureSingleByType = false)
    {
        foreach (var @event in events.SafeNull())
        {
            this.Register(@event, ensureSingleByType);
        }

        return this;
    }

    /// <summary>
    /// Registers the domain event to publish.
    /// Domain Events are only registered on the aggregate root because it is ensuring the integrity of the aggregate as a whole.
    /// </summary>
    /// <param name="event">The event.</param>
    public DomainEvents Register(IDomainEvent @event, bool ensureSingleByType = false)
    {
        if (@event is null)
        {
            return this;
        }

        if (ensureSingleByType)
        {
            this.registrations = this.registrations.Where(r =>
                r.IsNotOfType(@event.GetType())).ToList();
        }

        this.registrations.Add(@event);
        return this;
    }

    public async Task DispatchAsync(IMediator mediator)
    {
        EnsureArg.IsNotNull(mediator, nameof(mediator));

        foreach (var @event in this.GetAll())
        {
            await mediator.Publish(@event).AnyContext();
        }

        this.Clear();
    }

    /// <summary>
    /// Clears the registered domain events.
    /// </summary>
    public void Clear()
    {
        this.registrations.Clear();
    }
}
