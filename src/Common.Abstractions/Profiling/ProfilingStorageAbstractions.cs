// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Selects independent history datasets for maintenance.</summary>
/// <example><code>var value = ProfilingDataSet.All;</code></example>
public enum ProfilingDataSet
{
    /// <summary>All classification.</summary>
    /// <example><code>var value = ProfilingDataSet.All;</code></example>
    All,
    /// <summary>Runtime classification.</summary>
    /// <example><code>var value = ProfilingDataSet.Runtime;</code></example>
    Runtime,
    /// <summary>Operations classification.</summary>
    /// <example><code>var value = ProfilingDataSet.Operations;</code></example>
    Operations,
}

/// <summary>Distinguishes known persistence, administrative loss and uncertain commits.</summary>
/// <example><code>var value = ProfilingWriteOutcome.Accepted;</code></example>
public enum ProfilingWriteOutcome
{
    /// <summary>Accepted classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.Accepted;</code></example>
    Accepted,
    /// <summary>AlreadyStored classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.AlreadyStored;</code></example>
    AlreadyStored,
    /// <summary>AlreadySettled classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.AlreadySettled;</code></example>
    AlreadySettled,
    /// <summary>Cleared classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.Cleared;</code></example>
    Cleared,
    /// <summary>StaleWriter classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.StaleWriter;</code></example>
    StaleWriter,
    /// <summary>RetentionRejected classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.RetentionRejected;</code></example>
    RetentionRejected,
    /// <summary>CapacityRejected classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.CapacityRejected;</code></example>
    CapacityRejected,
    /// <summary>TransientFailure classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.TransientFailure;</code></example>
    TransientFailure,
    /// <summary>PermanentFailure classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.PermanentFailure;</code></example>
    PermanentFailure,
    /// <summary>UnknownCommit classification.</summary>
    /// <example><code>var value = ProfilingWriteOutcome.UnknownCommit;</code></example>
    UnknownCommit,
}

/// <summary>Describes recoverable clear maintenance progress.</summary>
/// <example><code>var value = ProfilingClearState.Preparing;</code></example>
public enum ProfilingClearState
{
    /// <summary>Preparing classification.</summary>
    /// <example><code>var value = ProfilingClearState.Preparing;</code></example>
    Preparing,
    /// <summary>Applying classification.</summary>
    /// <example><code>var value = ProfilingClearState.Applying;</code></example>
    Applying,
    /// <summary>Completed classification.</summary>
    /// <example><code>var value = ProfilingClearState.Completed;</code></example>
    Completed,
    /// <summary>Failed classification.</summary>
    /// <example><code>var value = ProfilingClearState.Failed;</code></example>
    Failed,
}

/// <summary>Reports storage scope and optional consistency functions.</summary>
/// <example><code>var value = new ProfilingProviderCapabilities();</code></example>
public sealed record ProfilingProviderCapabilities
{
    /// <summary>Gets the provider identifier.</summary>
    /// <example><code>var value = record.Name;</code></example>
    public string Name { get; init; }

    /// <summary>Gets the storage scope identity.</summary>
    /// <example><code>var value = record.Scope;</code></example>
    public string Scope { get; init; }

    /// <summary>Gets whether other nodes share this scope.</summary>
    /// <example><code>var value = record.Shared;</code></example>
    public bool Shared { get; init; }

    /// <summary>Gets stable publication-boundary support.</summary>
    /// <example><code>var value = record.SnapshotConsistentQueries;</code></example>
    public bool SnapshotConsistentQueries { get; init; } = true;

}

/// <summary>Selects history by dataset and optional half-open completion-UTC interval.</summary>
/// <example><code>var value = new ProfilingClearRequest();</code></example>
public sealed record ProfilingClearRequest
{
    /// <summary>Gets the dataset selector.</summary>
    /// <example><code>var value = record.DataSet;</code></example>
    public ProfilingDataSet DataSet { get; init; } = ProfilingDataSet.All;

    /// <summary>Gets the inclusive completion-UTC lower bound.</summary>
    /// <example><code>var value = record.FromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets the exclusive completion-UTC upper bound.</summary>
    /// <example><code>var value = record.ToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? ToUtc { get; init; }

}

/// <summary>Carries original provider-issued identity and authoritative expiry.</summary>
/// <example><code>var value = new ProfilingWriterLease();</code></example>
public sealed record ProfilingWriterLease
{
    /// <summary>Gets the unique writer lease ID.</summary>
    /// <example><code>var value = record.WriterId;</code></example>
    public Guid WriterId { get; init; }

    /// <summary>Gets the opaque lease token.</summary>
    /// <example><code>var value = record.Token;</code></example>
    public Guid Token { get; init; }

    /// <summary>Gets the persistent store incarnation.</summary>
    /// <example><code>var value = record.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; init; }

    /// <summary>Gets writer registration generation.</summary>
    /// <example><code>var value = record.Generation;</code></example>
    public long Generation { get; init; }

    /// <summary>Gets the executing process identity.</summary>
    /// <example><code>var value = record.NodeId;</code></example>
    public Guid NodeId { get; init; }

    /// <summary>Gets authoritative expiry UTC.</summary>
    /// <example><code>var value = record.ExpiresUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ExpiresUtc { get; init; }

    /// <summary>Gets confirmed non-replayable completion progress.</summary>
    /// <example><code>var value = record.SettledThrough;</code></example>
    public long SettledThrough { get; init; }

}

/// <summary>Retries one stable open attempt without creating duplicate leases.</summary>
/// <example><code>var value = new ProfilingOpenWriterRequest();</code></example>
public sealed record ProfilingOpenWriterRequest
{
    /// <summary>Gets client-generated stable attempt identity.</summary>
    /// <example><code>var value = record.AttemptId;</code></example>
    public Guid AttemptId { get; init; }

    /// <summary>Gets the cached process descriptor.</summary>
    /// <example><code>var value = record.Node;</code></example>
    public ProfilingNode Node { get; init; }

    /// <summary>Gets requested bounded lease duration.</summary>
    /// <example><code>var value = record.LeaseDuration;</code></example>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(30);

}

/// <summary>Renews a lease and fences settled sequence numbers atomically.</summary>
/// <example><code>var value = new ProfilingWriterSynchronizationRequest();</code></example>
public sealed record ProfilingWriterSynchronizationRequest
{
    /// <summary>Gets original lease identity.</summary>
    /// <example><code>var value = record.Lease;</code></example>
    public ProfilingWriterLease Lease { get; init; }

    /// <summary>Gets all completed numbers no longer retry eligible.</summary>
    /// <example><code>var value = record.SettledThrough;</code></example>
    public long SettledThrough { get; init; }

    /// <summary>Gets renewal duration.</summary>
    /// <example><code>var value = record.LeaseDuration;</code></example>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(30);

}

/// <summary>Asks an eligible writer to acknowledge one fixed local cutoff.</summary>
/// <example><code>var value = new ProfilingPendingClear();</code></example>
public sealed record ProfilingPendingClear
{
    /// <summary>Gets the maintenance occurrence ID.</summary>
    /// <example><code>var value = record.Id;</code></example>
    public Guid Id { get; init; }

    /// <summary>Gets the selected history.</summary>
    /// <example><code>var value = record.Selection;</code></example>
    public ProfilingClearRequest Selection { get; init; }

    /// <summary>Gets provider preparation UTC.</summary>
    /// <example><code>var value = record.PreparedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset PreparedUtc { get; init; }

    /// <summary>Gets preparation expiry UTC.</summary>
    /// <example><code>var value = record.DeadlineUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset DeadlineUtc { get; init; }

}

/// <summary>Returns lease validity, renewal and pending clear coordination.</summary>
/// <example><code>var value = new ProfilingWriterSynchronizationResult();</code></example>
public sealed record ProfilingWriterSynchronizationResult
{
    /// <summary>Gets whether writes remain admissible.</summary>
    /// <example><code>var value = record.Active;</code></example>
    public bool Active { get; init; }

    /// <summary>Gets authoritative renewed state.</summary>
    /// <example><code>var value = record.Lease;</code></example>
    public ProfilingWriterLease Lease { get; init; }

    /// <summary>Gets bounded clear requests for this lease.</summary>
    /// <example><code>var value = record.PendingClears;</code></example>
    public IReadOnlyList<ProfilingPendingClear> PendingClears { get; init; } = [];

}

/// <summary>Acknowledges a fixed completion cutoff and never advances it on retry.</summary>
/// <example><code>var value = new ProfilingClearAcknowledgement();</code></example>
public sealed record ProfilingClearAcknowledgement
{
    /// <summary>Gets the preparation ID.</summary>
    /// <example><code>var value = record.ClearId;</code></example>
    public Guid ClearId { get; init; }

    /// <summary>Gets eligible original lease.</summary>
    /// <example><code>var value = record.Lease;</code></example>
    public ProfilingWriterLease Lease { get; init; }

    /// <summary>Gets locally atomic last-published completion sequence.</summary>
    /// <example><code>var value = record.CompletionCutoff;</code></example>
    public long CompletionCutoff { get; init; }

}

/// <summary>Preserves immutable record identity across attempts and lease replacement.</summary>
/// <example><code>var value = new ProfilingWriteEnvelope();</code></example>
public sealed record ProfilingWriteEnvelope
{
    /// <summary>Gets original lease identity.</summary>
    /// <example><code>var value = record.Lease;</code></example>
    public ProfilingWriterLease Lease { get; init; }

    /// <summary>Gets writer-local publication sequence.</summary>
    /// <example><code>var value = record.CompletionSequence;</code></example>
    public long CompletionSequence { get; init; }

    /// <summary>Gets the frozen owned graph.</summary>
    /// <example><code>var value = record.Record;</code></example>
    public OperationProfilingRecord Record { get; init; }

}

/// <summary>Reports persistence disposition for one occurrence.</summary>
/// <example><code>var value = new ProfilingRecordWriteResult();</code></example>
public sealed record ProfilingRecordWriteResult
{
    /// <summary>Gets occurrence identity.</summary>
    /// <example><code>var value = record.OperationId;</code></example>
    public Guid OperationId { get; init; }

    /// <summary>Gets writer-local publication sequence.</summary>
    /// <example><code>var value = record.CompletionSequence;</code></example>
    public long CompletionSequence { get; init; }

    /// <summary>Gets known or uncertain write disposition.</summary>
    /// <example><code>var value = record.Outcome;</code></example>
    public ProfilingWriteOutcome Outcome { get; init; }

    /// <summary>Gets provider publication order when persisted.</summary>
    /// <example><code>var value = record.CommitWatermark;</code></example>
    public long? CommitWatermark { get; init; }

    /// <summary>Gets a bounded classification without backend error payload.</summary>
    /// <example><code>var value = record.SafeCode;</code></example>
    public string SafeCode { get; init; }

}

/// <summary>Returns one disposition per submitted root without claiming batch atomicity.</summary>
/// <example><code>var value = new ProfilingBatchWriteResult();</code></example>
public sealed record ProfilingBatchWriteResult
{
    /// <summary>Gets per-root atomic results.</summary>
    /// <example><code>var value = record.Records;</code></example>
    public IReadOnlyList<ProfilingRecordWriteResult> Records { get; init; } = [];

}

/// <summary>Bounds each provider maintenance call and never reads the full history.</summary>
/// <example><code>var value = new ProfilingMaintenanceRequest();</code></example>
public sealed record ProfilingMaintenanceRequest
{
    /// <summary>Gets maximum roots deleted per call.</summary>
    /// <example><code>var value = record.MaximumRoots;</code></example>
    public int MaximumRoots { get; init; } = 512;

    /// <summary>Gets the next-batch scheduling budget.</summary>
    /// <example><code>var value = record.Budget;</code></example>
    public TimeSpan Budget { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets operation count retention bound.</summary>
    /// <example><code>var value = record.MaximumOperationCount;</code></example>
    public int MaximumOperationCount { get; init; } = 10000;

    /// <summary>Gets optional memory payload retention bound.</summary>
    /// <example><code>var value = record.MaximumOperationBytes;</code></example>
    public long? MaximumOperationBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>Gets completion age retention bound.</summary>
    /// <example><code>var value = record.MaximumOperationAge;</code></example>
    public TimeSpan MaximumOperationAge { get; init; } = TimeSpan.FromHours(24);

}

/// <summary>Reports bounded recovery and retention progress.</summary>
/// <example><code>var value = new ProfilingMaintenanceResult();</code></example>
public sealed record ProfilingMaintenanceResult
{
    /// <summary>Gets terminal operation roots removed.</summary>
    /// <example><code>var value = record.RemovedOperations;</code></example>
    public long RemovedOperations { get; init; }

    /// <summary>Gets terminal runtime roots removed.</summary>
    /// <example><code>var value = record.RemovedRuntimeSessions;</code></example>
    public long RemovedRuntimeSessions { get; init; }

    /// <summary>Gets unfinished recoverable clears.</summary>
    /// <example><code>var value = record.RemainingClears;</code></example>
    public long RemainingClears { get; init; }

    /// <summary>Gets cursor-invalidating store revision.</summary>
    /// <example><code>var value = record.DeletionRevision;</code></example>
    public long DeletionRevision { get; init; }

}

/// <summary>Composes focused facets under one replaceable profiling provider.</summary>
/// <example><code>await provider.ClearAsync(new ProfilingClearRequest { DataSet = ProfilingDataSet.Operations }, cancellationToken);</code></example>
public interface IProfilingStorageProvider
{
    /// <summary>Gets explicit backend identity, scope and optional capabilities.</summary>
    /// <example><code>var scope = provider.Capabilities.Scope;</code></example>
    ProfilingProviderCapabilities Capabilities { get; }
    /// <summary>Gets the Runtime lifecycle store.</summary>
    /// <example><code>var sessions = await provider.Runtime.ListSessionsAsync();</code></example>
    IRuntimeProfilingStore Runtime { get; }
    /// <summary>Gets operation persistence and queries.</summary>
    /// <example><code>var record = await provider.Operations.FindAsync(id, cancellationToken);</code></example>
    IOperationProfilingStore Operations { get; }
    /// <summary>Fences queued and remote late writes before clearing selected complete root graphs.</summary>
    /// <example><code>var result = await provider.ClearAsync(new ProfilingClearRequest(), cancellationToken);</code></example>
    Task<IResult<ProfilingClearResult>> ClearAsync(ProfilingClearRequest request, CancellationToken cancellationToken = default);
    /// <summary>Resumes sealed clears, safe coordination cleanup and bounded retention.</summary>
    /// <example><code>await provider.ResumeMaintenanceAsync(new ProfilingMaintenanceRequest(), cancellationToken);</code></example>
    Task<IResult<ProfilingMaintenanceResult>> ResumeMaintenanceAsync(ProfilingMaintenanceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Defines provider-authoritative writer coordination and operation read semantics.</summary>
/// <example><code>var lease = await store.OpenWriterAsync(new ProfilingOpenWriterRequest { AttemptId = Guid.NewGuid(), Node = node });</code></example>
public interface IOperationProfilingStore
{
    /// <summary>Opens a bounded writer lease idempotently under a stable client attempt ID.</summary>
    /// <example><code>var lease = await store.OpenWriterAsync(request, cancellationToken);</code></example>
    Task<IResult<ProfilingWriterLease>> OpenWriterAsync(ProfilingOpenWriterRequest request, CancellationToken cancellationToken = default);
    /// <summary>Renews an unexpired lease and atomically settles non-replayable sequence numbers.</summary>
    /// <example><code>var state = await store.SynchronizeWriterAsync(request, cancellationToken);</code></example>
    Task<IResult<ProfilingWriterSynchronizationResult>> SynchronizeWriterAsync(ProfilingWriterSynchronizationRequest request, CancellationToken cancellationToken = default);
    /// <summary>Stores or returns the original acknowledgement, never widening the clear cutoff.</summary>
    /// <example><code>var cutoff = await store.AcknowledgeClearAsync(acknowledgement, cancellationToken);</code></example>
    Task<IResult<ProfilingClearAcknowledgement>> AcknowledgeClearAsync(ProfilingClearAcknowledgement request, CancellationToken cancellationToken = default);
    /// <summary>Appends atomic root graphs with original lease, settlement, fences and idempotency checks.</summary>
    /// <example><code>var batch = await store.AppendAsync(envelopes, cancellationToken);</code></example>
    Task<IResult<ProfilingBatchWriteResult>> AppendAsync(IReadOnlyList<ProfilingWriteEnvelope> records, CancellationToken cancellationToken = default);
    /// <summary>Retires an original lease permanently, including after coordination cleanup.</summary>
    /// <example><code>await store.CloseWriterAsync(lease, cancellationToken);</code></example>
    Task<IResult> CloseWriterAsync(ProfilingWriterLease lease, CancellationToken cancellationToken = default);
    /// <summary>Looks up one occurrence independently of list windows or default outcomes.</summary>
    /// <example><code>var record = await store.FindAsync(id, cancellationToken);</code></example>
    Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Filters and pages at a stable published boundary with validated typed predicates.</summary>
    /// <example><code>var page = await store.QueryAsync(query, cancellationToken);</code></example>
    Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default);
    /// <summary>Counts and ranks groups in storage before applying a bounded group selection.</summary>
    /// <example><code>var groups = await store.GroupAsync(query, cancellationToken);</code></example>
    Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default);
    /// <summary>Selects bounded exact analysis inputs; oversized selections fail without silent truncation.</summary>
    /// <example><code>var records = await store.SelectAnalysisAsync(query, cancellationToken);</code></example>
    Task<IResult<OperationProfilingAnalysisSelection>> SelectAnalysisAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default);
}
