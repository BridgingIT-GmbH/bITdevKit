// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>An immutable provider expression tree compiled from the existing FilterModel.</summary>
/// <example><code>var value = new AuditPredicate();</code></example>
public sealed record AuditPredicate
{
    /// <summary>True, And, Or, Comparison, Collection, or ResourceMatch.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; } = "True";

    /// <summary>Record, Action, Change, Resource, or Property field domain.</summary>
    /// <example><code>var value = snapshot.Domain;</code></example>
    public string Domain { get; init; } = "Record";

    /// <summary>Catalog-approved field name.</summary>
    /// <example><code>var value = snapshot.Field;</code></example>
    public string Field { get; init; }

    /// <summary>Catalog-approved owned collection.</summary>
    /// <example><code>var value = snapshot.Collection;</code></example>
    public string Collection { get; init; }

    /// <summary>Validated scalar or collection operator.</summary>
    /// <example><code>var value = snapshot.Operator;</code></example>
    public FilterOperator Operator { get; init; } = FilterOperator.Equal;

    /// <summary>Typed immutable comparison value.</summary>
    /// <example><code>var value = snapshot.Value;</code></example>
    public AuditQueryValue Value { get; init; } = new();

    /// <summary>Complete nested predicates, never just the first child.</summary>
    /// <example><code>var value = snapshot.Children;</code></example>
    public ImmutableArray<AuditPredicate> Children { get; init; } = [];

    /// <summary>Mandatory typed resource tuple when selecting history.</summary>
    /// <example><code>var value = snapshot.Resource;</code></example>
    public AuditResourceReference Resource { get; init; }
}
