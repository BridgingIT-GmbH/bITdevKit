// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Describes one property value captured during a successful entity change transaction.
/// </summary>
/// <example>
/// <code>
/// var change = EntityChangeHistoryAccessor.GetPendingChangeSets(customer)
///     .Single()
///     .PropertyChanges
///     .Single(c =&gt; c.PropertyName == nameof(Customer.Name));
/// </code>
/// </example>
public sealed class EntityPropertyChange
{
    /// <summary>
    /// Gets or sets the zero-based capture order within the containing change set.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the changed property name.
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the optional full property path for owned values or graph members.
    /// </summary>
    public string PropertyPath { get; set; }

    /// <summary>
    /// Gets or sets the property value before the first change in the change set.
    /// </summary>
    public object OldValue { get; set; }

    /// <summary>
    /// Gets or sets the final property value after the change set completed.
    /// </summary>
    public object NewValue { get; set; }

    /// <summary>
    /// Gets or sets the CLR type token for the changed value.
    /// </summary>
    public string ValueClrType { get; set; }
}
