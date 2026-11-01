// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>An approved grouping over exactly the scoped selection.</summary>
/// <example><code>var value = new AuditAggregateQuery();</code></example>
public sealed record AuditAggregateQuery
{
    /// <summary>Validated effective selection.</summary>
    /// <example><code>var value = snapshot.Selection;</code></example>
    public AuditSearchPlan Selection { get; init; } = new();

    /// <summary>Catalog-approved root grouping field.</summary>
    /// <example><code>var value = snapshot.Field;</code></example>
    public string Field { get; init; } = "Outcome";

    /// <summary>Maximum returned groups.</summary>
    /// <example><code>var value = snapshot.Limit;</code></example>
    public int Limit { get; init; } = 50;
}
