// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>A bounded selection of immutable records linked by explicit origin or cause.</summary>
/// <example><code>var value = new AuditRelatedExecutionsQuery();</code></example>
public sealed record AuditRelatedExecutionsQuery
{
    /// <summary>Validated effective selection including trusted scope.</summary>
    /// <example><code>var value = snapshot.Selection;</code></example>
    public AuditSearchPlan Selection { get; init; } = new();

    /// <summary>Authorized anchor execution.</summary>
    /// <example><code>var value = snapshot.AnchorAuditId;</code></example>
    public Guid AnchorAuditId { get; init; }

    /// <summary>Authorized anchor business-trail identity.</summary>
    /// <example><code>var value = snapshot.OriginAuditId;</code></example>
    public Guid OriginAuditId { get; init; }

    /// <summary>Explicit permitted parent identities, not correlation guesses.</summary>
    /// <example><code>var value = snapshot.ParentIds;</code></example>
    public ImmutableArray<Guid> ParentIds { get; init; } = [];
}
