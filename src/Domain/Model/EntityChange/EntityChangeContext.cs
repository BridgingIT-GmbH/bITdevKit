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

    /// <summary>
    /// Records a change to a specific property.
    /// </summary>
    internal void RecordChange(string propertyName, object oldValue, object newValue)
    {
        this.changes[propertyName] = (oldValue, newValue);
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
