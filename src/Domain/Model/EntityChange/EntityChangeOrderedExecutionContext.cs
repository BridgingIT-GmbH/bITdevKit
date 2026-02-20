// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using BridgingIT.DevKit.Domain;
using System;
using System.Collections.Generic;

/// <summary>
/// Context for ordered operation execution, tracking changes and queued events.
/// </summary>
internal class EntityChangeOrderedExecutionContext<TEntity>
{
    /// <summary>
    /// Gets a value indicating whether any operations have made changes to the entity.
    /// </summary>
    public bool ChangesMade { get; set; }

    /// <summary>
    /// Gets the dictionary storing old values of changed properties for event factories.
    /// </summary>
    public Dictionary<string, object> OldValues { get; } = [];

    /// <summary>
    /// Gets the list of queued event factories with their optional target aggregates to be registered at the end of Apply().
    /// </summary>
    public List<(Func<IDomainEvent> EventFactory, IAggregateRoot Target)> QueuedEvents { get; } = [];

    /// <summary>
    /// Gets the list of queued OnChanged actions to be executed at the end of Apply().
    /// </summary>
    public List<Action<TEntity>> QueuedOnChangedActions { get; } = [];

    /// <summary>
    /// Records a property change with its old value.
    /// </summary>
    public void RecordChange(string propertyName, object oldValue)
    {
        this.ChangesMade = true;
        this.OldValues[propertyName] = oldValue;
    }

    /// <summary>
    /// Queues an event factory to be registered when Apply() completes successfully.
    /// </summary>
    /// <param name="eventFactory">The factory function that creates the domain event.</param>
    /// <param name="target">Optional target aggregate root on which to register the event. If null, uses the entity itself.</param>
    public void QueueEvent(Func<IDomainEvent> eventFactory, IAggregateRoot target = null)
    {
        this.QueuedEvents.Add((eventFactory, target));
    }

    /// <summary>
    /// Gets the old value of a changed property for use in event factories.
    /// </summary>
    public T GetOldValue<T>(string propertyName)
    {
        if (this.OldValues.TryGetValue(propertyName, out var value))
        {
            return (T)value;
        }
        return default;
    }
}
