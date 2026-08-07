// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;

/// <summary>
/// Describes the property changes produced by one successful <c>EntityChangeBuilder.Apply</c> call.
/// </summary>
/// <example>
/// <code>
/// var result = customer.Change().Set(c =&gt; c.Name, "New name").Apply();
/// var changeSet = EntityChangeHistoryAccessor.GetPendingChangeSets(customer).Single();
/// </code>
/// </example>
public sealed class EntityChangeSet
{
    /// <summary>
    /// Gets or sets the stable identifier shared by all property changes in this transaction.
    /// </summary>
    public Guid ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the short CLR type name of the changed entity.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the diagnostic CLR type token of the changed entity.
    /// </summary>
    public string EntityClrType { get; set; }

    /// <summary>
    /// Gets or sets the string representation of the changed entity id.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the CLR type token of the changed entity id.
    /// </summary>
    public string EntityIdType { get; set; }

    /// <summary>
    /// Gets or sets the captured property changes in declaration order.
    /// </summary>
    public IReadOnlyList<EntityPropertyChange> PropertyChanges { get; set; } = [];
}
