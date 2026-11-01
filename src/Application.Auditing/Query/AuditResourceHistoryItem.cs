// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>One execution plus a bounded summary of exact typed-resource action matches.</summary>
/// <example><code>var value = new AuditResourceHistoryItem();</code></example>
public sealed record AuditResourceHistoryItem
{
    /// <summary>Execution summary under the same access/lifecycle selection.</summary>
    /// <example><code>var value = snapshot.Record;</code></example>
    public AuditRecordSummary Record { get; init; } = new();

    /// <summary>Retained matching occurrences, bounded independently of total count.</summary>
    /// <example><code>var value = snapshot.MatchingActions;</code></example>
    public ImmutableArray<AuditMatchingAction> MatchingActions { get; init; } = [];

    /// <summary>All retained matching actions before the summary limit.</summary>
    /// <example><code>var value = snapshot.MatchingActionCount;</code></example>
    public long MatchingActionCount { get; init; }

    /// <summary>Whether the root itself references the exact resource tuple.</summary>
    /// <example><code>var value = snapshot.RootResourceMatched;</code></example>
    public bool RootResourceMatched { get; init; }
}
