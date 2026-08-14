// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Collections.Generic;

/// <summary>
/// A context object containing details about the changes applied during a specific change transaction.
/// Used primarily within custom domain event factories to access previous values.
/// </summary>
public class EntityChangeContext
{
    private readonly Dictionary<string, (object OldValue, object NewValue)> changes = [];
    private readonly List<EntityPropertyChange> propertyChanges = [];

    /// <summary>
    /// Gets the read-only list of property changes captured for the transaction.
    /// </summary>
    /// <example>
    /// <code>
    /// if (context.Changes.Any(c =&gt; c.PropertyName == nameof(Customer.Email)))
    /// {
    ///     // Build an email-specific domain event.
    /// }
    /// </code>
    /// </example>
    public IReadOnlyList<EntityPropertyChange> Changes => this.propertyChanges;

    /// <summary>
    /// Records a change to a specific property.
    /// </summary>
    internal void RecordChange(string propertyName, object oldValue, object newValue)
    {
        this.changes[propertyName] = (oldValue, newValue);
    }

    /// <summary>
    /// Records a captured property change.
    /// </summary>
    internal void RecordChange(EntityPropertyChange change)
    {
        if (change is null)
        {
            return;
        }

        this.changes[change.PropertyName] = (change.OldValue, change.NewValue);
        this.propertyChanges.Add(change);
    }

    /// <summary>
    /// Checks if a specific property was changed during the transaction.
    /// </summary>
    public bool HasChanged(string propertyName) => this.changes.ContainsKey(propertyName);

    /// <summary>
    /// Gets the old value of a modified property.
    /// </summary>
    public T GetOldValue<T>(string propertyName)
    {
        if (this.changes.TryGetValue(propertyName, out var record))
        {
            return (T)record.OldValue;
        }

        return default;
    }

    /// <summary>
    /// Gets the new value of a modified property.
    /// </summary>
    public T GetNewValue<T>(string propertyName)
    {
        if (this.changes.TryGetValue(propertyName, out var record))
        {
            return (T)record.NewValue;
        }

        return default;
    }
}
