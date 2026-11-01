// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Business summary without original child payloads or a false integrity claim.</summary>
/// <example><code>var value = new AuditRecordSummary();</code></example>
public sealed record AuditRecordSummary
{
    /// <summary>Execution identity.</summary>
    /// <example><code>var value = snapshot.AuditId;</code></example>
    public Guid AuditId { get; init; }

    /// <summary>Business key.</summary>
    /// <example><code>var value = snapshot.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Captured business label.</summary>
    /// <example><code>var value = snapshot.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Captured execution kind.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Observed UTC start.</summary>
    /// <example><code>var value = snapshot.StartedUtc;</code></example>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Observed UTC completion.</summary>
    /// <example><code>var value = snapshot.CompletedUtc;</code></example>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>Observed root outcome.</summary>
    /// <example><code>var value = snapshot.Outcome;</code></example>
    public AuditOutcome Outcome { get; init; }

    /// <summary>Captured instrumentation completeness.</summary>
    /// <example><code>var value = snapshot.Completeness;</code></example>
    public AuditCompleteness Completeness { get; init; }

    /// <summary>Captured executing actor.</summary>
    /// <example><code>var value = snapshot.Actor;</code></example>
    public AuditActor Actor { get; init; } = new();

    /// <summary>Captured trusted initiating actor.</summary>
    /// <example><code>var value = snapshot.InitiatingActor;</code></example>
    public AuditActor InitiatingActor { get; init; }

    /// <summary>Captured entry module.</summary>
    /// <example><code>var value = snapshot.ModuleName;</code></example>
    public string ModuleName { get; init; }

    /// <summary>Original application correlation identity.</summary>
    /// <example><code>var value = snapshot.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Business trail origin identity.</summary>
    /// <example><code>var value = snapshot.OriginAuditId;</code></example>
    public Guid OriginAuditId { get; init; }

    /// <summary>Immediate initiating execution.</summary>
    /// <example><code>var value = snapshot.CausationAuditId;</code></example>
    public Guid? CausationAuditId { get; init; }

    /// <summary>Provider lifecycle metadata, outside the protected document.</summary>
    /// <example><code>var value = snapshot.StorageState;</code></example>
    public string StorageState { get; init; } = "Active";

    /// <summary>Summary reads do not claim source verification.</summary>
    /// <example><code>var value = snapshot.IntegrityStatus;</code></example>
    public string IntegrityStatus { get; init; } = "NotChecked";

    /// <summary>Total observed action admission attempts.</summary>
    /// <example><code>var value = snapshot.ActionCount;</code></example>
    public long ActionCount { get; init; }

    /// <summary>Retained action occurrences.</summary>
    /// <example><code>var value = snapshot.RetainedActionCount;</code></example>
    public long RetainedActionCount { get; init; }

    /// <summary>Retained evidence groups.</summary>
    /// <example><code>var value = snapshot.ChangeCount;</code></example>
    public long ChangeCount { get; init; }

    /// <summary>Whether retained child failures were observed.</summary>
    /// <example><code>var value = snapshot.HasFailedActions;</code></example>
    public bool HasFailedActions { get; init; }

    /// <summary>Query as-of timestamp.</summary>
    /// <example><code>var value = snapshot.AsOfUtc;</code></example>
    public DateTimeOffset AsOfUtc { get; init; }
}
