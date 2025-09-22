// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using System.Linq;

/// <summary>
/// Abstract base class for entities implementing the Active Entity pattern, embedding CRUD and query operations.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId>
    where TEntity : ActiveEntity<TEntity, TId> // recursive type bound: The base knows the derived type (TEntity) inherits from itself.
{
    private readonly List<IDomainEvent> domainEvents = [];
    private readonly Lock domainEventsLock = new(); // Dedicated lock object for thread safety

    protected TEntity Self => (TEntity)this;

    /// <summary>
    /// Registers a domain event. This operation is thread-safe.
    /// </summary>
    /// <param name="event">The domain event to register.</param>
    public void RegisterDomainEvent(IDomainEvent @event)
    {
        this.RegisterDomainEvent(@event, ensureSingleByType: false);
    }

    /// <summary>
    /// Gets the domain events for this entity (read-only). This operation is thread-safe,
    /// returning a snapshot of the events.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyCollection<IDomainEvent> DomainEvents
    {
        get
        {
            lock (this.domainEventsLock)
            {
                // Return a new list to prevent external modification of the internal list
                return this.domainEvents.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Registers a domain event, optionally ensuring only one event of the same type exists. This operation is thread-safe.
    /// </summary>
    /// <param name="event">The domain event to register.</param>
    /// <param name="ensureSingleByType">If true, removes any existing event of the same type before adding the new one.</param>
    public void RegisterDomainEvent(IDomainEvent @event, bool ensureSingleByType)
    {
        if (@event == null)
        {
            return;
        }

        lock (this.domainEventsLock)
        {
            if (ensureSingleByType)
            {
                this.domainEvents.RemoveAll(e => e.GetType() == @event.GetType());
            }

            if (!this.domainEvents.Contains(@event))
            {
                this.domainEvents.Add(@event);
            }
        }
    }

    /// <summary>
    /// Retrieves all registered domain events. This operation is thread-safe,
    /// returning a new list (snapshot) of the events.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> GetDomainEvents()
    {
        lock (this.domainEventsLock)
        {
            // Return a new list to prevent external modification of the internal list
            return this.domainEvents.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Clears all registered domain events. This operation is thread-safe.
    /// </summary>
    public void ClearDomainEvents()
    {
        lock (this.domainEventsLock)
        {
            this.domainEvents.Clear();
        }
    }

    /// <summary>
    /// Checks if any domain events are registered. This operation is thread-safe.
    /// </summary>
    public bool HasDomainEvents()
    {
        lock (this.domainEventsLock)
        {
            return this.domainEvents.Count > 0;
        }
    }

    /// <summary>
    /// Checks if a specific domain event is registered. This operation is thread-safe.
    /// </summary>
    /// <typeparam name="TDomainEvent">The type of the domain event to check for.</typeparam>
    public bool HasDomainEvent<TDomainEvent>() where TDomainEvent : IDomainEvent
    {
        lock (this.domainEventsLock)
        {
            return this.domainEvents.Any(e => e is TDomainEvent);
        }
    }

    /// <summary>
    /// Publishes all registered domain events for this entity using the specified publisher.
    /// This method implicitly accesses the domain events in a thread-safe manner via <see cref="GetDomainEvents"/>.
    /// </summary>
    /// <param name="publisher">The domain event publisher.</param>
    /// <param name="options">Optional publishing options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task<IResult> PublishDomainEventsAsync(
        IDomainEventPublisher publisher,
        ActiveEntityDomainEventPublishOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (publisher == null)
        {
            return Result.Failure("publisher cannot be null.");
        }

        // ActiveEntityDomainEventCollector will call GetDomainEvents() and ClearDomainEvents(),
        return await new ActiveEntityDomainEventCollector()
            .PublishAllAsync(this, publisher, options, cancellationToken);
    }

    /// <summary>
    /// Publishes all registered domain events for this entity using the specified notifier.
    /// This method implicitly accesses the domain events in a thread-safe manner via <see cref="GetDomainEvents"/>.
    /// </summary>
    /// <param name="notifier">The notifier.</param>
    /// <param name="options">Optional publishing options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task<IResult> PublishDomainEventsAsync(
        INotifier notifier,
        ActiveEntityDomainEventPublishOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (notifier == null)
        {
            return Result.Failure("notifier cannot be null.");
        }

        // ActiveEntityDomainEventCollector will call GetDomainEvents() and ClearDomainEvents(),
        return await new ActiveEntityDomainEventCollector()
            .PublishAllAsync(this, notifier, options, cancellationToken);
    }
}