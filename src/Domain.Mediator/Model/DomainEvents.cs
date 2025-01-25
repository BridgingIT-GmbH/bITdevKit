// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents a collection of domain events that can be registered, retrieved, and managed for an aggregate root.
/// </summary>
public class DomainEvents // TODO: create interface?
{
    /// <summary>
    ///     A collection of domain events that have been registered for an aggregate root.
    ///     This collection enables the registration, retrieval, and management of domain events,
    ///     ensuring the integrity and consistency of the aggregate root.
    /// </summary>
    private ICollection<IDomainEvent> registrations = []; // TODO: concurrent collection?

    /// <summary>
    ///     Retrieves all currently registered domain events.
    /// </summary>
    /// <returns>An enumerable collection of registered domain events.</returns>
    public IEnumerable<IDomainEvent> GetAll()
    {
        return this.registrations;
    }

    /// <summary>
    ///     Registers the domain events to publish.
    ///     Domain Events are only registered on the aggregate root because it ensures the integrity of the aggregate as a
    ///     whole.
    /// </summary>
    /// <param name="events">The events to register.</param>
    /// <param name="ensureSingleByType">Optional parameter to ensure a single event type. Default is false.</param>
    /// <returns>The updated DomainEvents instance.</returns>
    public DomainEvents Register(IEnumerable<IDomainEvent> events, bool ensureSingleByType = false)
    {
        foreach (var @event in events.SafeNull())
        {
            this.Register(@event, ensureSingleByType);
        }

        return this;
    }

    /// <summary>
    ///     Registers the domain event to be published, optionally ensuring a single instance of the event type.
    /// </summary>
    /// <param name="event">The event to register.</param>
    /// <param name="ensureSingleByType">
    ///     True to ensure only a single instance of the event type is registered, false
    ///     otherwise.
    /// </param>
    /// <return>The updated DomainEvents instance.</return>
    public DomainEvents Register(IDomainEvent @event, bool ensureSingleByType = false)
    {
        if (@event is null)
        {
            return this;
        }

        if (ensureSingleByType)
        {
            this.registrations = this.registrations.Where(r => r.IsNotOfType(@event.GetType())).ToList();
        }

        this.registrations.Add(@event);

        return this;
    }

    /// <summary>
    ///     Dispatches all registered domain events asynchronously using the provided mediator.
    /// </summary>
    /// <param name="mediator">The mediator used to publish the events.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
    ///     Clears the registered domain events.
    /// </summary>
    public void Clear()
    {
        this.registrations.Clear();
    }
}