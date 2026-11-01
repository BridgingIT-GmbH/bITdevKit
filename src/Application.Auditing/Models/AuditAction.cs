// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>A frozen action occurrence owned by one execution.</summary>
/// <example><code>var value = new AuditAction();</code></example>
public sealed record AuditAction
{
    /// <summary>Action occurrence identity.</summary>
    /// <example><code>var value = snapshot.Id;</code></example>
    public Guid Id { get; init; }

    /// <summary>Parent occurrence, distinct from chronological ordering.</summary>
    /// <example><code>var value = snapshot.ParentActionId;</code></example>
    public Guid? ParentActionId { get; init; }

    /// <summary>Admission sequence for deterministic display.</summary>
    /// <example><code>var value = snapshot.Sequence;</code></example>
    public long Sequence { get; init; }

    /// <summary>Stable business/source operation key.</summary>
    /// <example><code>var value = snapshot.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Frozen business label.</summary>
    /// <example><code>var value = snapshot.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Business or source-specific action kind.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; } = "Business";

    /// <summary>Approved business reason.</summary>
    /// <example><code>var value = snapshot.Reason;</code></example>
    public string Reason { get; init; }

    /// <summary>Owning source feature.</summary>
    /// <example><code>var value = snapshot.Feature;</code></example>
    public string Feature { get; init; }

    /// <summary>Module current during this action.</summary>
    /// <example><code>var value = snapshot.ModuleName;</code></example>
    public string ModuleName { get; init; }

    /// <summary>Captured application correlation identifier.</summary>
    /// <example><code>var value = snapshot.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Qualified request identity, without a payload.</summary>
    /// <example><code>var value = snapshot.RequestType;</code></example>
    public string RequestType { get; init; }

    /// <summary>Qualified invoked handler identity.</summary>
    /// <example><code>var value = snapshot.HandlerType;</code></example>
    public string HandlerType { get; init; }

    /// <summary>Observed retry attempt, never an inferred count.</summary>
    /// <example><code>var value = snapshot.Attempt;</code></example>
    public int? Attempt { get; init; }

    /// <summary>Invoked, ShortCircuited, Skipped, or Incomplete.</summary>
    /// <example><code>var value = snapshot.ExecutionStatus;</code></example>
    public string ExecutionStatus { get; init; } = "Invoked";

    /// <summary>UTC action start.</summary>
    /// <example><code>var value = snapshot.StartedUtc;</code></example>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>UTC action completion.</summary>
    /// <example><code>var value = snapshot.CompletedUtc;</code></example>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>Observed action outcome, independent of its root.</summary>
    /// <example><code>var value = snapshot.Outcome;</code></example>
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Unknown;

    /// <summary>Frozen approved resource references.</summary>
    /// <example><code>var value = snapshot.Resources;</code></example>
    public ImmutableArray<AuditResourceReference> Resources { get; init; } = [];

    /// <summary>Frozen projected values and policy markers.</summary>
    /// <example><code>var value = snapshot.Details;</code></example>
    public ImmutableDictionary<string, AuditValue> Details { get; init; } = ImmutableDictionary.Create<string, AuditValue>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Safe exception occurrence references.</summary>
    /// <example><code>var value = snapshot.Errors;</code></example>
    public ImmutableArray<AuditSafeError> Errors { get; init; } = [];

}
