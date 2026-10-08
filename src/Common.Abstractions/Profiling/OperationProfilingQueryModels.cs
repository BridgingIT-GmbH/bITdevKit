// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Orders completed operation observations and groups.</summary>
/// <example><code>var value = OperationProfilingView.Slow;</code></example>
public enum OperationProfilingView
{
    /// <summary>Slow classification.</summary>
    /// <example><code>var value = OperationProfilingView.Slow;</code></example>
    Slow,
    /// <summary>Recent classification.</summary>
    /// <example><code>var value = OperationProfilingView.Recent;</code></example>
    Recent,
    /// <summary>ByCount classification.</summary>
    /// <example><code>var value = OperationProfilingView.ByCount;</code></example>
    ByCount,
}

/// <summary>Defines bounded scalar predicates; Mixed applies to segment summaries only.</summary>
/// <example><code>var selection = ProfilingDimensionOperator.Equal;</code></example>
public enum ProfilingDimensionOperator
{
    /// <summary>Matches the exact type and canonical scalar value.</summary>
    /// <example><code>var selection = ProfilingDimensionOperator.Equal;</code></example>
    Equal,
    /// <summary>Matches observed presence without claiming every invocation supplied the value.</summary>
    /// <example><code>var selection = ProfilingDimensionOperator.Present;</code></example>
    Present,
    /// <summary>Matches absence from the retained metadata.</summary>
    /// <example><code>var selection = ProfilingDimensionOperator.Missing;</code></example>
    Missing,
    /// <summary>Matches differing values within the selected summary.</summary>
    /// <example><code>var selection = ProfilingDimensionOperator.Mixed;</code></example>
    Mixed,
}

/// <summary>Matches a canonical dimension name and exact scalar type/value.</summary>
/// <example><code>var value = new ProfilingDimensionPredicate();</code></example>
public sealed record ProfilingDimensionPredicate
{
    /// <summary>Gets the case-insensitive metadata name.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the exact typed value.</summary>
    /// <example><code>var value = record.Value;</code></example>
    public ProfilingValue Value { get; init; }

    /// <summary>Gets equality, observed presence, absence or mixed-summary selection.</summary>
    /// <example><code>var predicate = new ProfilingDimensionPredicate { Key = "category", Operator = ProfilingDimensionOperator.Present };</code></example>
    public ProfilingDimensionOperator Operator { get; init; } = ProfilingDimensionOperator.Equal;

}

/// <summary>Pins publication order, scope, filters, deletion revision and expiry.</summary>
/// <example><code>var value = new ProfilingQueryBoundary();</code></example>
public sealed record ProfilingQueryBoundary
{
    /// <summary>Gets the evaluated inclusive UTC lower bound.</summary>
    /// <example><code>var value = record.FromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets the evaluated exclusive UTC upper bound.</summary>
    /// <example><code>var value = record.ToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Gets the provider authentication tag for immutable boundary values.</summary>
    /// <example><code>var value = record.Signature;</code></example>
    public string Signature { get; init; }

    /// <summary>Gets persistent store incarnation.</summary>
    /// <example><code>var value = record.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; init; }

    /// <summary>Gets the maximum published insertion order.</summary>
    /// <example><code>var value = record.CommitWatermark;</code></example>
    public long CommitWatermark { get; init; }

    /// <summary>Gets the observed invalidation revision.</summary>
    /// <example><code>var value = record.DeletionRevision;</code></example>
    public long DeletionRevision { get; init; }

    /// <summary>Gets immutable validated-filter identity.</summary>
    /// <example><code>var value = record.FilterFingerprint;</code></example>
    public string FilterFingerprint { get; init; }

    /// <summary>Gets cursor expiry UTC.</summary>
    /// <example><code>var value = record.ExpiresUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ExpiresUtc { get; init; }

}

/// <summary>Defines bounded completion-time filters and stable paging.</summary>
/// <example><code>var value = new OperationProfilingQuery();</code></example>
public sealed record OperationProfilingQuery
{
    /// <summary>Gets an optional occurrence identifier filter.</summary>
    /// <example><code>var value = record.Id;</code></example>
    public Guid? Id { get; init; }

    /// <summary>Gets an optional non-owning parent identifier filter.</summary>
    /// <example><code>var value = record.ParentOperationId;</code></example>
    public Guid? ParentOperationId { get; init; }

    /// <summary>Gets an ordinal application version filter.</summary>
    /// <example><code>var value = record.ApplicationVersion;</code></example>
    public string ApplicationVersion { get; init; }

    /// <summary>Gets whether the UTC window selects observed interval overlap for Runtime navigation.</summary>
    /// <example><code>var value = record.IntervalOverlap;</code></example>
    public bool IntervalOverlap { get; init; }

    /// <summary>Gets an observed failed-segment filter, independent of root outcome.</summary>
    /// <example><code>var value = record.HasSegmentFailures;</code></example>
    public bool? HasSegmentFailures { get; init; }

    /// <summary>Gets a case-insensitive leaf key filter.</summary>
    /// <example><code>var value = record.SegmentKey;</code></example>
    public string SegmentKey { get; init; }

    /// <summary>Gets an exact structured full-path filter.</summary>
    /// <example><code>var value = record.SegmentPath;</code></example>
    public ProfilingSegmentPath SegmentPath { get; init; }

    /// <summary>Gets an HTTP method filter.</summary>
    /// <example><code>var value = record.HttpMethod;</code></example>
    public string HttpMethod { get; init; }

    /// <summary>Gets an original endpoint route filter.</summary>
    /// <example><code>var value = record.Route;</code></example>
    public string Route { get; init; }

    /// <summary>Gets a final observable HTTP status filter.</summary>
    /// <example><code>var value = record.HttpStatusCode;</code></example>
    public int? HttpStatusCode { get; init; }

    /// <summary>Gets the HTTP application request identifier.</summary>
    /// <example><code>var value = record.ApplicationRequestId;</code></example>
    public string ApplicationRequestId { get; init; }

    /// <summary>Gets a request sampling strategy filter.</summary>
    /// <example><code>var value = record.SamplingStrategyKey;</code></example>
    public string SamplingStrategyKey { get; init; }

    /// <summary>Gets an effective sampling configuration filter.</summary>
    /// <example><code>var value = record.SamplingConfigurationKey;</code></example>
    public string SamplingConfigurationKey { get; init; }

    /// <summary>Gets optional method grouping in addition to kind/key.</summary>
    /// <example><code>var value = record.GroupByHttpMethod;</code></example>
    public bool GroupByHttpMethod { get; init; }

    /// <summary>Gets an opaque provider-authenticated continuation key.</summary>
    /// <example><code>var value = record.Cursor;</code></example>
    public string Cursor { get; init; }

    /// <summary>Gets predicates that must all match the same selected segment summary.</summary>
    /// <example><code>var predicates = query.SegmentDimensions;</code></example>
    public IReadOnlyList<ProfilingDimensionPredicate> SegmentDimensions { get; init; } = [];

    /// <summary>Gets selected observed invocation outcomes on the matching segment path.</summary>
    /// <example><code>var outcomes = query.SegmentOutcomes;</code></example>
    public IReadOnlyList<ProfilingSegmentOutcome> SegmentOutcomes { get; init; } = [];

    /// <summary>Gets inclusive completion UTC; service defaults to fifteen minutes.</summary>
    /// <example><code>var value = record.FromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets exclusive completion UTC.</summary>
    /// <example><code>var value = record.ToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Gets an optional invariant execution-kind filter.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Gets an optional invariant operation-key filter.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets an optional executing process filter.</summary>
    /// <example><code>var value = record.NodeId;</code></example>
    public Guid? NodeId { get; init; }

    /// <summary>Gets lookup-only correlation filtering.</summary>
    /// <example><code>var value = record.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Gets selected observed outcomes.</summary>
    /// <example><code>var value = record.Outcomes;</code></example>
    public IReadOnlyList<OperationProfilingOutcome> Outcomes { get; init; } = [OperationProfilingOutcome.Completed];

    /// <summary>Gets at most eight typed predicates.</summary>
    /// <example><code>var value = record.Dimensions;</code></example>
    public IReadOnlyList<ProfilingDimensionPredicate> Dimensions { get; init; } = [];

    /// <summary>Gets at most four dimension grouping names.</summary>
    /// <example><code>var value = record.GroupingDimensions;</code></example>
    public IReadOnlyList<string> GroupingDimensions { get; init; } = [];

    /// <summary>Gets Slow, Recent or ByCount mode.</summary>
    /// <example><code>var value = record.View;</code></example>
    public OperationProfilingView View { get; init; } = OperationProfilingView.Slow;

    /// <summary>Gets the bounded page size.</summary>
    /// <example><code>var value = record.PageSize;</code></example>
    public int PageSize { get; init; } = 50;

    /// <summary>Gets the exact analysis bound.</summary>
    /// <example><code>var value = record.MaximumAnalysisCount;</code></example>
    public int MaximumAnalysisCount { get; init; } = 10000;

    /// <summary>Gets an optional existing publication boundary.</summary>
    /// <example><code>var value = record.Boundary;</code></example>
    public ProfilingQueryBoundary Boundary { get; init; }

}

/// <summary>Returns immutable occurrences at one publication boundary.</summary>
/// <example><code>var value = new OperationProfilingPage();</code></example>
public sealed record OperationProfilingPage
{
    /// <summary>Gets an opaque stable continuation key when more results exist.</summary>
    /// <example><code>var value = record.NextCursor;</code></example>
    public string NextCursor { get; init; }

    /// <summary>Gets selected operation roots.</summary>
    /// <example><code>var value = record.Records;</code></example>
    public IReadOnlyList<OperationProfilingRecord> Records { get; init; } = [];

    /// <summary>Gets matched stored root count.</summary>
    /// <example><code>var value = record.TotalCount;</code></example>
    public long TotalCount { get; init; }

    /// <summary>Gets stable follow-up paging boundary.</summary>
    /// <example><code>var value = record.Boundary;</code></example>
    public ProfilingQueryBoundary Boundary { get; init; }

    /// <summary>Gets whether further matching roots exist.</summary>
    /// <example><code>var value = record.HasMore;</code></example>
    public bool HasMore { get; init; }

}

/// <summary>Reports matching sample count and deterministic ranking within one logical group.</summary>
/// <example><code>var value = new OperationProfilingGroup();</code></example>
public sealed record OperationProfilingGroup
{
    /// <summary>Gets whether cumulative duration exceeded its representation.</summary>
    /// <example><code>if (group.DurationUnavailable) ShowUnavailable();</code></example>
    public bool DurationUnavailable { get; init; }

    /// <summary>Gets the selected method group when enabled.</summary>
    /// <example><code>var value = record.HttpMethod;</code></example>
    public string HttpMethod { get; init; }

    /// <summary>Gets the unambiguous portable identity of this group.</summary>
    /// <example><code>var value = record.ComparisonKey;</code></example>
    public string ComparisonKey { get; init; }

    /// <summary>Gets selected real occurrences for Slow/Recent, rather than fabricated aggregate bars.</summary>
    /// <example><code>var rows = group.Occurrences;</code></example>
    public IReadOnlyList<OperationProfilingRecord> Occurrences { get; init; } = [];

    /// <summary>Gets the retained display operation key.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the retained execution kind.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Gets selected grouping characteristics.</summary>
    /// <example><code>var value = record.Dimensions;</code></example>
    public IReadOnlyList<ProfilingDimension> Dimensions { get; init; } = [];

    /// <summary>Gets matching stored operation count.</summary>
    /// <example><code>var value = record.Count;</code></example>
    public long Count { get; init; }

    /// <summary>Gets cumulative operation duration.</summary>
    /// <example><code>var value = record.TotalDuration;</code></example>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Gets shortest matched observation.</summary>
    /// <example><code>var value = record.MinimumDuration;</code></example>
    public TimeSpan MinimumDuration { get; init; }

    /// <summary>Gets longest matched observation.</summary>
    /// <example><code>var value = record.MaximumDuration;</code></example>
    public TimeSpan MaximumDuration { get; init; }

    /// <summary>Gets latest observed completion.</summary>
    /// <example><code>var value = record.LatestCompletedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset LatestCompletedUtc { get; init; }

    /// <summary>Gets one selected slowest or recent root for the bar.</summary>
    /// <example><code>var value = record.Representative;</code></example>
    public OperationProfilingRecord Representative { get; init; }

}

/// <summary>Returns bounded groups and total counts without exposing database queries.</summary>
/// <example><code>var value = new OperationProfilingGroupPage();</code></example>
public sealed record OperationProfilingGroupPage
{
    /// <summary>Gets an opaque stable continuation key when more results exist.</summary>
    /// <example><code>var value = record.NextCursor;</code></example>
    public string NextCursor { get; init; }

    /// <summary>Gets selected ranked groups.</summary>
    /// <example><code>var value = record.Groups;</code></example>
    public IReadOnlyList<OperationProfilingGroup> Groups { get; init; } = [];

    /// <summary>Gets matched group count.</summary>
    /// <example><code>var value = record.TotalGroupCount;</code></example>
    public long TotalGroupCount { get; init; }

    /// <summary>Gets matched stored sample count.</summary>
    /// <example><code>var value = record.TotalOperationCount;</code></example>
    public long TotalOperationCount { get; init; }

    /// <summary>Gets stable publication boundary.</summary>
    /// <example><code>var value = record.Boundary;</code></example>
    public ProfilingQueryBoundary Boundary { get; init; }

    /// <summary>Gets further group availability.</summary>
    /// <example><code>var value = record.HasMore;</code></example>
    public bool HasMore { get; init; }

}

/// <summary>Returns exact bounded inputs for deterministic distributions and comparison.</summary>
/// <example><code>var value = new OperationProfilingAnalysisSelection();</code></example>
public sealed record OperationProfilingAnalysisSelection
{
    /// <summary>Gets all selected bounded observations.</summary>
    /// <example><code>var value = record.Records;</code></example>
    public IReadOnlyList<OperationProfilingRecord> Records { get; init; } = [];

    /// <summary>Gets stable selection boundary.</summary>
    /// <example><code>var value = record.Boundary;</code></example>
    public ProfilingQueryBoundary Boundary { get; init; }

}

/// <summary>Reports observed-duration statistics with explicit sample size.</summary>
/// <example><code>var value = new OperationProfilingDistribution();</code></example>
public sealed record OperationProfilingDistribution
{
    /// <summary>Gets contributing observation count.</summary>
    /// <example><code>var value = record.Count;</code></example>
    public long Count { get; init; }

    /// <summary>Gets minimum duration.</summary>
    /// <example><code>var value = record.MinimumMilliseconds;</code></example>
    public double MinimumMilliseconds { get; init; }

    /// <summary>Gets maximum duration.</summary>
    /// <example><code>var value = record.MaximumMilliseconds;</code></example>
    public double MaximumMilliseconds { get; init; }

    /// <summary>Gets mean duration.</summary>
    /// <example><code>var value = record.MeanMilliseconds;</code></example>
    public double MeanMilliseconds { get; init; }

    /// <summary>Gets nearest-rank median.</summary>
    /// <example><code>var value = record.P50Milliseconds;</code></example>
    public double P50Milliseconds { get; init; }

    /// <summary>Gets nearest-rank p95.</summary>
    /// <example><code>var value = record.P95Milliseconds;</code></example>
    public double P95Milliseconds { get; init; }

    /// <summary>Gets nearest-rank p99.</summary>
    /// <example><code>var value = record.P99Milliseconds;</code></example>
    public double P99Milliseconds { get; init; }

}

/// <summary>Keeps both sample sets visible without pretending paired or causal measurements.</summary>
/// <example><code>var value = new OperationProfilingComparison();</code></example>
public sealed record OperationProfilingComparison
{
    /// <summary>Gets baseline observed statistics.</summary>
    /// <example><code>var value = record.Baseline;</code></example>
    public OperationProfilingDistribution Baseline { get; init; }

    /// <summary>Gets candidate observed statistics.</summary>
    /// <example><code>var value = record.Candidate;</code></example>
    public OperationProfilingDistribution Candidate { get; init; }

    /// <summary>Gets relative p95 change when baseline is nonzero.</summary>
    /// <example><code>var value = record.P95ChangePercent;</code></example>
    public double? P95ChangePercent { get; init; }

}

/// <summary>Relates independent Runtime evidence by cached process identity and observed interval.</summary>
/// <example><code>var value = new OperationProfilingRuntimeOverlay();</code></example>
public sealed record OperationProfilingRuntimeOverlay
{
    /// <summary>Gets the non-owning selected occurrence.</summary>
    /// <example><code>var value = record.OperationId;</code></example>
    public Guid OperationId { get; init; }

    /// <summary>Gets overlapping observed samples without interpolation.</summary>
    /// <example><code>var value = record.Snapshots;</code></example>
    public IReadOnlyList<RuntimeProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets contributing independent session links.</summary>
    /// <example><code>var value = record.Sessions;</code></example>
    public IReadOnlyList<RuntimeProfilingSession> Sessions { get; init; } = [];

    /// <summary>Gets whether relevant evidence exists.</summary>
    /// <example><code>var value = record.Available;</code></example>
    public bool Available { get; init; }

    /// <summary>Gets explicit missing, sparse or skewed evidence.</summary>
    /// <example><code>var value = record.Limitation;</code></example>
    public string Limitation { get; init; }

}

/// <summary>Reports capture quality, bounded buffers and persisted-data freshness.</summary>
/// <example><code>var value = new OperationProfilingHealth();</code></example>
public sealed record OperationProfilingHealth
{
    /// <summary>Gets queued records currently owned by one active provider attempt.</summary>
    /// <example><code>var active = health.InFlightRecords;</code></example>
    public long InFlightRecords { get; init; }

    /// <summary>Gets charged bytes owned by the active provider attempt, included in total queue bytes.</summary>
    /// <example><code>var bytes = health.InFlightPayloadBytes;</code></example>
    public long InFlightPayloadBytes { get; init; }

    /// <summary>Gets the node-local QueueOldestAge observation.</summary>
    /// <example><code>var value = health.QueueOldestAge;</code></example>
    public TimeSpan QueueOldestAge { get; init; }

    /// <summary>Indicates that the queue clock could not supply its oldest-record age; the duration must not be interpreted.</summary>
    /// <example><code>if (health.QueueOldestAgeUnavailable) { ShowUnavailable(); }</code></example>
    public bool QueueOldestAgeUnavailable { get; init; }

    /// <summary>Gets the node-local WriterStalled observation.</summary>
    /// <example><code>var value = health.WriterStalled;</code></example>
    public bool WriterStalled { get; init; }

    /// <summary>Gets the node-local PersistenceFaults observation.</summary>
    /// <example><code>var value = health.PersistenceFaults;</code></example>
    public long PersistenceFaults { get; init; }

    /// <summary>Gets the node-local RetryAttempts observation.</summary>
    /// <example><code>var value = health.RetryAttempts;</code></example>
    public long RetryAttempts { get; init; }

    /// <summary>Gets the node-local LastAttemptUtc observation.</summary>
    /// <example><code>var value = health.LastAttemptUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset LastAttemptUtc { get; init; }

    /// <summary>Gets intentional node-local path exclusions, outside sampling evaluation.</summary>
    /// <example><code>var excluded = health.BlacklistedRequests;</code></example>
    public long BlacklistedRequests { get; init; }

    /// <summary>Gets intentional node-local sampling skips, distinct from sampler errors and recording loss.</summary>
    /// <example><code>var skipped = health.SamplingSkippedRequests;</code></example>
    public long SamplingSkippedRequests { get; init; }

    /// <summary>Gets the effective process-local HTTP sampling strategy, absent without the HTTP adapter.</summary>
    /// <example><code>var strategy = health.SamplingStrategyKey;</code></example>
    public string SamplingStrategyKey { get; init; }

    /// <summary>Gets the effective process-local HTTP sampling settings identifier.</summary>
    /// <example><code>var settings = health.SamplingConfigurationKey;</code></example>
    public string SamplingConfigurationKey { get; init; }

    /// <summary>Gets the node-local EligibleRequests observation.</summary>
    /// <example><code>var value = health.EligibleRequests;</code></example>
    public long EligibleRequests { get; init; }

    /// <summary>Gets the node-local SelectedRequests observation.</summary>
    /// <example><code>var value = health.SelectedRequests;</code></example>
    public long SelectedRequests { get; init; }

    /// <summary>Gets the node-local SamplingErrors observation.</summary>
    /// <example><code>var value = health.SamplingErrors;</code></example>
    public long SamplingErrors { get; init; }

    /// <summary>Gets the node-local RetentionRemovals observation.</summary>
    /// <example><code>var value = health.RetentionRemovals;</code></example>
    public long RetentionRemovals { get; init; }

    /// <summary>Gets the node-local CountersNodeId observation.</summary>
    /// <example><code>var value = health.CountersNodeId;</code></example>
    public Guid CountersNodeId { get; init; }

    /// <summary>Gets the node-local ProviderShared observation.</summary>
    /// <example><code>var value = health.ProviderShared;</code></example>
    public bool ProviderShared { get; init; }

    /// <summary>Gets the node-local CaptureEnabled observation.</summary>
    /// <example><code>var value = health.CaptureEnabled;</code></example>
    public bool CaptureEnabled { get; init; }

    /// <summary>Gets cumulative admitted roots.</summary>
    /// <example><code>var value = record.AdmittedOperations;</code></example>
    public long AdmittedOperations { get; init; }

    /// <summary>Gets capacity-rejected roots.</summary>
    /// <example><code>var value = record.RejectedOperations;</code></example>
    public long RejectedOperations { get; init; }

    /// <summary>Gets finalized observed roots.</summary>
    /// <example><code>var value = record.CompletedOperations;</code></example>
    public long CompletedOperations { get; init; }

    /// <summary>Gets currently admitted roots.</summary>
    /// <example><code>var value = record.ActiveOperations;</code></example>
    public int ActiveOperations { get; init; }

    /// <summary>Gets retained active payload.</summary>
    /// <example><code>var value = record.ActivePayloadBytes;</code></example>
    public long ActivePayloadBytes { get; init; }

    /// <summary>Gets queued plus in-flight roots.</summary>
    /// <example><code>var value = record.QueueRecords;</code></example>
    public long QueueRecords { get; init; }

    /// <summary>Gets queued plus in-flight payload.</summary>
    /// <example><code>var value = record.QueuePayloadBytes;</code></example>
    public long QueuePayloadBytes { get; init; }

    /// <summary>Gets known accepted roots.</summary>
    /// <example><code>var value = record.PersistedOperations;</code></example>
    public long PersistedOperations { get; init; }

    /// <summary>Gets known capture or persistence losses.</summary>
    /// <example><code>var value = record.DroppedOperations;</code></example>
    public long DroppedOperations { get; init; }

    /// <summary>Gets roots discarded under clear fences.</summary>
    /// <example><code>var value = record.AdministrativeDiscards;</code></example>
    public long AdministrativeDiscards { get; init; }

    /// <summary>Gets abandoned writes whose commit could not be established.</summary>
    /// <example><code>var value = record.UnknownCommits;</code></example>
    public long UnknownCommits { get; init; }

    /// <summary>Gets completions before lease admission.</summary>
    /// <example><code>var value = record.WriterUnavailableDiscards;</code></example>
    public long WriterUnavailableDiscards { get; init; }

    /// <summary>Gets lost envelopes under stale original leases.</summary>
    /// <example><code>var value = record.ExpiredLeaseDiscards;</code></example>
    public long ExpiredLeaseDiscards { get; init; }

    /// <summary>Gets isolated recorder or logger faults.</summary>
    /// <example><code>var value = record.CaptureFaults;</code></example>
    public long CaptureFaults { get; init; }

    /// <summary>Gets most recent known successful publication.</summary>
    /// <example><code>var value = record.LastSuccessfulFlushUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset LastSuccessfulFlushUtc { get; init; }

    /// <summary>Gets explicit node-local or shared scope.</summary>
    /// <example><code>var value = record.ProviderScope;</code></example>
    public string ProviderScope { get; init; }

    /// <summary>Gets current lease admission availability.</summary>
    /// <example><code>var value = record.WriterActive;</code></example>
    public bool WriterActive { get; init; }

    /// <summary>Gets clear preparations.</summary>
    /// <example><code>var value = record.PreparingClears;</code></example>
    public long PreparingClears { get; init; }

    /// <summary>Gets sealed incomplete deletions.</summary>
    /// <example><code>var value = record.ApplyingClears;</code></example>
    public long ApplyingClears { get; init; }

}

/// <summary>Builds bounded operation analysis independently of dashboard transport.</summary>
/// <example><code>var groups = await queries.GroupAsync(new OperationProfilingQuery(), cancellationToken);</code></example>
public interface IOperationProfilingQueryService
{
    /// <summary>Reads a stable operation page under one non-waiting query admission.</summary>
    /// <example><code>var page = await queries.QueryAsync(query, cancellationToken);</code></example>
    Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default);
    /// <summary>Reads bounded Slow, Recent or ByCount groups.</summary>
    /// <example><code>var groups = await queries.GroupAsync(query, cancellationToken);</code></example>
    Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default);
    /// <summary>Looks up one occurrence without list-window restrictions.</summary>
    /// <example><code>var record = await queries.FindAsync(id, cancellationToken);</code></example>
    Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Computes exact bounded observed distributions.</summary>
    /// <example><code>var distribution = await queries.AnalyzeAsync(query, cancellationToken);</code></example>
    Task<IResult<OperationProfilingDistribution>> AnalyzeAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default);
    /// <summary>Compares independently selected observations and their sample counts.</summary>
    /// <example><code>var comparison = await queries.CompareAsync(baseline, candidate, cancellationToken);</code></example>
    Task<IResult<OperationProfilingComparison>> CompareAsync(OperationProfilingQuery baseline, OperationProfilingQuery candidate, CancellationToken cancellationToken = default);
    /// <summary>Relates bounded overlapping Runtime evidence without ownership or deletion coupling.</summary>
    /// <example><code>var overlay = await queries.GetRuntimeOverlayAsync(id, cancellationToken);</code></example>
    Task<IResult<OperationProfilingRuntimeOverlay>> GetRuntimeOverlayAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Reads local capture health; queries never force a persistence flush.</summary>
    /// <example><code>var health = queries.GetHealth();</code></example>
    OperationProfilingHealth GetHealth();
}
