// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Bounded scoped suggestions over approved root fields.</summary>
/// <example><code>var value = new AuditFacetQuery();</code></example>
public sealed record AuditFacetQuery
{
    /// <summary>Validated effective selection.</summary>
    /// <example><code>var value = snapshot.Selection;</code></example>
    public AuditSearchPlan Selection { get; init; } = new();

    /// <summary>Catalog-approved suggestion field.</summary>
    /// <example><code>var value = snapshot.Field;</code></example>
    public string Field { get; init; } = "Key";

    /// <summary>Optional bounded prefix.</summary>
    /// <example><code>var value = snapshot.Prefix;</code></example>
    public string Prefix { get; init; }

    /// <summary>Maximum distinct suggestions.</summary>
    /// <example><code>var value = snapshot.Limit;</code></example>
    public int Limit { get; init; } = 20;
}
