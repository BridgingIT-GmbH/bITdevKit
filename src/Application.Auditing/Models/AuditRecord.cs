// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>A finalized immutable logical audit document, independent of storage lifecycle and delivery state.</summary>
/// <example><code>var value = new AuditRecord();</code></example>
public sealed record AuditRecord
{
    /// <summary>Retained actions whose observed final outcome failed.</summary>
    /// <example><code>var failed = snapshot.FailedActionCount;</code></example>
    public long FailedActionCount { get; init; }

    /// <summary>Whether captured action failures occurred without replacing the root outcome.</summary>
    /// <example><code>var failed = snapshot.HasFailedActions;</code></example>
    public bool HasFailedActions { get; init; }

    /// <summary>Stable execution identity assigned before admission.</summary>
    /// <example><code>var value = snapshot.AuditId;</code></example>
    public Guid AuditId { get; init; }

    /// <summary>Captured document schema version.</summary>
    /// <example><code>var value = snapshot.SchemaVersion;</code></example>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Frozen business/source execution key.</summary>
    /// <example><code>var value = snapshot.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Frozen business label.</summary>
    /// <example><code>var value = snapshot.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Business or source-specific execution kind.</summary>
    /// <example><code>var value = snapshot.Kind;</code></example>
    public string Kind { get; init; } = "Business";

    /// <summary>Explicit approved business reason.</summary>
    /// <example><code>var value = snapshot.Reason;</code></example>
    public string Reason { get; init; }

    /// <summary>Source application identity.</summary>
    /// <example><code>var value = snapshot.Application;</code></example>
    public string Application { get; init; }

    /// <summary>Source process/node identity.</summary>
    /// <example><code>var value = snapshot.Node;</code></example>
    public string Node { get; init; }

    /// <summary>Entry module, when available.</summary>
    /// <example><code>var value = snapshot.ModuleName;</code></example>
    public string ModuleName { get; init; }

    /// <summary>Trusted initiating module across worker boundaries.</summary>
    /// <example><code>var value = snapshot.InitiatingModuleName;</code></example>
    public string InitiatingModuleName { get; init; }

    /// <summary>Original application correlation identifier.</summary>
    /// <example><code>var value = snapshot.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Identity of the execution that began the causal trail.</summary>
    /// <example><code>var value = snapshot.OriginAuditId;</code></example>
    public Guid OriginAuditId { get; init; }

    /// <summary>Immediate initiating execution identity.</summary>
    /// <example><code>var value = snapshot.CausationAuditId;</code></example>
    public Guid? CausationAuditId { get; init; }

    /// <summary>Immediate initiating action identity.</summary>
    /// <example><code>var value = snapshot.CausationActionId;</code></example>
    public Guid? CausationActionId { get; init; }

    /// <summary>Optional opaque source flow identity, distinct from correlation and causation.</summary>
    /// <example><code>var flow = snapshot.FlowId;</code></example>
    public string FlowId { get; init; }

    /// <summary>Captured source job identity when this execution is job-related.</summary>
    /// <example><code>var name = snapshot.JobName;</code></example>
    public string JobName { get; init; }

    /// <summary>Optional source request identity.</summary>
    /// <example><code>var value = snapshot.RequestId;</code></example>
    public string RequestId { get; init; }

    /// <summary>Optional trusted job occurrence identity.</summary>
    /// <example><code>var value = snapshot.JobOccurrenceId;</code></example>
    public string JobOccurrenceId { get; init; }

    /// <summary>Optional job execution identity.</summary>
    /// <example><code>var value = snapshot.JobExecutionId;</code></example>
    public string JobExecutionId { get; init; }

    /// <summary>UTC execution start.</summary>
    /// <example><code>var value = snapshot.StartedUtc;</code></example>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>UTC execution completion.</summary>
    /// <example><code>var value = snapshot.CompletedUtc;</code></example>
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>Observed root outcome; recovered child failures do not replace it.</summary>
    /// <example><code>var value = snapshot.Outcome;</code></example>
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Unknown;

    /// <summary>Completeness of admitted instrumentation.</summary>
    /// <example><code>var value = snapshot.Completeness;</code></example>
    public AuditCompleteness Completeness { get; init; } = AuditCompleteness.Complete;

    /// <summary>Executing actor captured before queued persistence.</summary>
    /// <example><code>var value = snapshot.Actor;</code></example>
    public AuditActor Actor { get; init; } = new();

    /// <summary>Separate trusted initiating actor.</summary>
    /// <example><code>var value = snapshot.InitiatingActor;</code></example>
    public AuditActor InitiatingActor { get; init; }

    /// <summary>Optional HTTP boundary metadata.</summary>
    /// <example><code>var value = snapshot.Http;</code></example>
    public AuditHttpContext Http { get; init; }

    /// <summary>Retained immutable action occurrences.</summary>
    /// <example><code>var value = snapshot.Actions;</code></example>
    public ImmutableArray<AuditAction> Actions { get; init; } = [];

    /// <summary>Retained optional source change evidence.</summary>
    /// <example><code>var value = snapshot.EntityChanges;</code></example>
    public ImmutableArray<AuditChangeEvidence> EntityChanges { get; init; } = [];

    /// <summary>Frozen root resources.</summary>
    /// <example><code>var value = snapshot.Resources;</code></example>
    public ImmutableArray<AuditResourceReference> Resources { get; init; } = [];

    /// <summary>Frozen root details.</summary>
    /// <example><code>var value = snapshot.Details;</code></example>
    public ImmutableDictionary<string, AuditValue> Details { get; init; } = ImmutableDictionary.Create<string, AuditValue>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Safe root exception references.</summary>
    /// <example><code>var value = snapshot.Errors;</code></example>
    public ImmutableArray<AuditSafeError> Errors { get; init; } = [];

    /// <summary>Safe aggregated capture/completeness notes.</summary>
    /// <example><code>var value = snapshot.Notes;</code></example>
    public ImmutableArray<AuditCaptureNote> Notes { get; init; } = [];

    /// <summary>All observed action admission attempts.</summary>
    /// <example><code>var value = snapshot.TotalActionCount;</code></example>
    public long TotalActionCount { get; init; }

    /// <summary>Action occurrences omitted by capture bounds.</summary>
    /// <example><code>var value = snapshot.OmittedActionCount;</code></example>
    public long OmittedActionCount { get; init; }

    /// <summary>Projected details omitted by capture bounds.</summary>
    /// <example><code>var value = snapshot.OmittedDetailCount;</code></example>
    public long OmittedDetailCount { get; init; }

    /// <summary>SHA-256 identity of the complete canonical immutable document.</summary>
    /// <example><code>var value = snapshot.ContentHash;</code></example>
    public string ContentHash { get; init; }

    /// <summary>Optional source authentication envelope; verification is computed on reads.</summary>
    /// <example><code>var value = snapshot.IntegrityProtection;</code></example>
    public AuditIntegrityProtection IntegrityProtection { get; init; }

    // Assembly-controlled bookkeeping prevents independent reassignment of
    // the typed projection and the original document used for authentication.
    [JsonIgnore]
    internal JsonElement OriginalDocument { get; init; }
    [JsonIgnore]
    internal string OriginalProjectionHash { get; init; }
}
