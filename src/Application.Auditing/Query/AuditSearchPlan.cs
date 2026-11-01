// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Immutable provider execution plan; HTTP callers continue to submit FilterModel directly.</summary>
/// <example><code>var value = new AuditSearchPlan();</code></example>
public sealed record AuditSearchPlan
{
    /// <summary>Whether the current trusted host decision explicitly permits unconstrained record access.</summary>
    /// <example><code>var globalFactsAllowed = plan.IsUnrestrictedScope;</code></example>
    public bool IsUnrestrictedScope { get; init; }

    /// <summary>Whether archive membership changes can affect this expression.</summary>
    /// <example><code>var stable = plan.RequiresStableLifecycle;</code></example>
    public bool RequiresStableLifecycle { get; init; } = true;

    /// <summary>Effective predicate including trusted outer AND constraints.</summary>
    /// <example><code>var value = snapshot.Predicate;</code></example>
    public AuditPredicate Predicate { get; init; } = new();

    /// <summary>Approved precedence with a stable AuditId tie-breaker.</summary>
    /// <example><code>var value = snapshot.Orderings;</code></example>
    public ImmutableArray<AuditOrder> Orderings { get; init; } = [];

    /// <summary>One-based page number.</summary>
    /// <example><code>var value = snapshot.Page;</code></example>
    public int Page { get; init; } = 1;

    /// <summary>Bounded page size.</summary>
    /// <example><code>var value = snapshot.PageSize;</code></example>
    public int PageSize { get; init; } = 50;

    /// <summary>Absolute event-range lower boundary, when applicable.</summary>
    /// <example><code>var value = snapshot.FromUtc;</code></example>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Absolute event-range upper boundary, when applicable.</summary>
    /// <example><code>var value = snapshot.ToUtc;</code></example>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Active, Archived, or All selected dataset.</summary>
    /// <example><code>var value = snapshot.Lifecycle;</code></example>
    public string Lifecycle { get; init; } = "Active";

    /// <summary>Canonical selection fingerprint for continuation binding.</summary>
    /// <example><code>var value = snapshot.Fingerprint;</code></example>
    public string Fingerprint { get; init; }

    /// <summary>Trusted scope/revision fingerprint.</summary>
    /// <example><code>var value = snapshot.ScopeFingerprint;</code></example>
    public string ScopeFingerprint { get; init; }

    /// <summary>Operation as-of time; paging remains a live view.</summary>
    /// <example><code>var value = snapshot.AsOfUtc;</code></example>
    public DateTimeOffset AsOfUtc { get; init; }
}
