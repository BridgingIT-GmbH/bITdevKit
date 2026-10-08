// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides standard execution kinds; custom identifiers use Kind on the start request.</summary>
/// <example><code>var value = OperationProfilingKind.HttpRequest;</code></example>
public enum OperationProfilingKind
{
    /// <summary>HttpRequest classification.</summary>
    /// <example><code>var value = OperationProfilingKind.HttpRequest;</code></example>
    HttpRequest,
    /// <summary>BlazorInteraction classification.</summary>
    /// <example><code>var value = OperationProfilingKind.BlazorInteraction;</code></example>
    BlazorInteraction,
    /// <summary>Service classification.</summary>
    /// <example><code>var value = OperationProfilingKind.Service;</code></example>
    Service,
    /// <summary>Job classification.</summary>
    /// <example><code>var value = OperationProfilingKind.Job;</code></example>
    Job,
    /// <summary>Pipeline classification.</summary>
    /// <example><code>var value = OperationProfilingKind.Pipeline;</code></example>
    Pipeline,
    /// <summary>Orchestration classification.</summary>
    /// <example><code>var value = OperationProfilingKind.Orchestration;</code></example>
    Orchestration,
    /// <summary>Custom classification.</summary>
    /// <example><code>var value = OperationProfilingKind.Custom;</code></example>
    Custom,
}

/// <summary>Describes the observed root outcome independently of capture quality.</summary>
/// <example><code>var value = OperationProfilingOutcome.Completed;</code></example>
public enum OperationProfilingOutcome
{
    /// <summary>Completed classification.</summary>
    /// <example><code>var value = OperationProfilingOutcome.Completed;</code></example>
    Completed,
    /// <summary>Failed classification.</summary>
    /// <example><code>var value = OperationProfilingOutcome.Failed;</code></example>
    Failed,
    /// <summary>Canceled classification.</summary>
    /// <example><code>var value = OperationProfilingOutcome.Canceled;</code></example>
    Canceled,
    /// <summary>Aborted classification.</summary>
    /// <example><code>var value = OperationProfilingOutcome.Aborted;</code></example>
    Aborted,
    /// <summary>Incomplete classification.</summary>
    /// <example><code>var value = OperationProfilingOutcome.Incomplete;</code></example>
    Incomplete,
}

/// <summary>Describes an individual segment invocation.</summary>
/// <example><code>var value = ProfilingSegmentOutcome.Completed;</code></example>
public enum ProfilingSegmentOutcome
{
    /// <summary>Completed classification.</summary>
    /// <example><code>var value = ProfilingSegmentOutcome.Completed;</code></example>
    Completed,
    /// <summary>Failed classification.</summary>
    /// <example><code>var value = ProfilingSegmentOutcome.Failed;</code></example>
    Failed,
    /// <summary>Canceled classification.</summary>
    /// <example><code>var value = ProfilingSegmentOutcome.Canceled;</code></example>
    Canceled,
    /// <summary>Incomplete classification.</summary>
    /// <example><code>var value = ProfilingSegmentOutcome.Incomplete;</code></example>
    Incomplete,
}

/// <summary>Explains why terminal execution was not observed.</summary>
/// <example><code>var value = ProfilingIncompleteReason.None;</code></example>
public enum ProfilingIncompleteReason
{
    /// <summary>None classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.None;</code></example>
    None,
    /// <summary>UnmarkedScope classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.UnmarkedScope;</code></example>
    UnmarkedScope,
    /// <summary>ParentClosed classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.ParentClosed;</code></example>
    ParentClosed,
    /// <summary>CaptureDeadlineExceeded classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.CaptureDeadlineExceeded;</code></example>
    CaptureDeadlineExceeded,
    /// <summary>HostStopping classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.HostStopping;</code></example>
    HostStopping,
    /// <summary>ClassificationFailed classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.ClassificationFailed;</code></example>
    ClassificationFailed,
    /// <summary>CaptureFault classification.</summary>
    /// <example><code>var value = ProfilingIncompleteReason.CaptureFault;</code></example>
    CaptureFault,
}

/// <summary>Classifies mutually exclusive observed root wall time.</summary>
/// <example><code>var value = ProfilingWallTimeKind.Segment;</code></example>
public enum ProfilingWallTimeKind
{
    /// <summary>Segment classification.</summary>
    /// <example><code>var value = ProfilingWallTimeKind.Segment;</code></example>
    Segment,
    /// <summary>Parallel classification.</summary>
    /// <example><code>var value = ProfilingWallTimeKind.Parallel;</code></example>
    Parallel,
    /// <summary>OutsideSegments classification.</summary>
    /// <example><code>var value = ProfilingWallTimeKind.OutsideSegments;</code></example>
    OutsideSegments,
}

/// <summary>Describes whether bytes or another adapter observation are complete.</summary>
/// <example><code>var value = ProfilingObservationQuality.Complete;</code></example>
public enum ProfilingObservationQuality
{
    /// <summary>Complete classification.</summary>
    /// <example><code>var value = ProfilingObservationQuality.Complete;</code></example>
    Complete,
    /// <summary>Partial classification.</summary>
    /// <example><code>var value = ProfilingObservationQuality.Partial;</code></example>
    Partial,
    /// <summary>Unavailable classification.</summary>
    /// <example><code>var value = ProfilingObservationQuality.Unavailable;</code></example>
    Unavailable,
}

/// <summary>Retains safe classification without exception objects or raw messages.</summary>
/// <example><code>var value = new ProfilingFailureDescriptor();</code></example>
public sealed record ProfilingFailureDescriptor
{
    /// <summary>Gets the stable source category.</summary>
    /// <example><code>var value = record.Source;</code></example>
    public string Source { get; init; }

    /// <summary>Gets the stable safe code.</summary>
    /// <example><code>var value = record.Code;</code></example>
    public string Code { get; init; }

    /// <summary>Gets the exception type when available.</summary>
    /// <example><code>var value = record.ExceptionType;</code></example>
    public string ExceptionType { get; init; }

    /// <summary>Gets an optional sanitized bounded message.</summary>
    /// <example><code>var value = record.Message;</code></example>
    public string Message { get; init; }

}

/// <summary>Separates diagnostic limitations from business outcomes.</summary>
/// <example><code>var value = new ProfilingCaptureQuality();</code></example>
public sealed record ProfilingCaptureQuality
{
    /// <summary>Gets whether capture exceeded a bound.</summary>
    /// <example><code>var value = record.Truncated;</code></example>
    public bool Truncated { get; init; }

    /// <summary>Gets the number of rejected metadata writes.</summary>
    /// <example><code>var value = record.RejectedMetadataCount;</code></example>
    public long RejectedMetadataCount { get; init; }

    /// <summary>Gets the number of rejected segment starts.</summary>
    /// <example><code>var value = record.RejectedSegmentCount;</code></example>
    public long RejectedSegmentCount { get; init; }

    /// <summary>Gets incomplete invocation count.</summary>
    /// <example><code>var value = record.IncompleteSegmentCount;</code></example>
    public long IncompleteSegmentCount { get; init; }

    /// <summary>Gets whether UTC and monotonic observations diverged.</summary>
    /// <example><code>var value = record.ClockDiscontinuity;</code></example>
    public bool ClockDiscontinuity { get; init; }

    /// <summary>Gets whether timing coverage is incomplete.</summary>
    /// <example><code>var value = record.PartialCoverage;</code></example>
    public bool PartialCoverage { get; init; }

    /// <summary>Gets whether duration is a lower bound on execution.</summary>
    /// <example><code>var value = record.ActualCompletionUnobserved;</code></example>
    public bool ActualCompletionUnobserved { get; init; }

    /// <summary>Gets whether a supplied classifier failed.</summary>
    /// <example><code>var value = record.ClassificationFailed;</code></example>
    public bool ClassificationFailed { get; init; }

    /// <summary>Gets the observation termination reason.</summary>
    /// <example><code>var value = record.IncompleteReason;</code></example>
    public ProfilingIncompleteReason IncompleteReason { get; init; }

}

/// <summary>Defines bounded root metadata without application payload retention.</summary>
/// <example><code>var value = new OperationProfilingStartRequest();</code></example>
public sealed record OperationProfilingStartRequest
{
    /// <summary>Gets the logical grouping key.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets a standard or host-defined bounded kind.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public string Kind { get; init; } = "Custom";

    /// <summary>Gets an optional display label.</summary>
    /// <example><code>var value = record.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Gets the lookup-only correlation ID.</summary>
    /// <example><code>var value = record.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Gets an optional non-owning parent link.</summary>
    /// <example><code>var value = record.ParentOperationId;</code></example>
    public Guid? ParentOperationId { get; init; }

}

/// <summary>Carries bounded typed source details under an adapter schema.</summary>
/// <example><code>var value = new ProfilingAdapterMetadata();</code></example>
public sealed record ProfilingAdapterMetadata
{
    /// <summary>Gets the adapter identifier.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Gets the schema version.</summary>
    /// <example><code>var value = record.SchemaVersion;</code></example>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Gets scalar source fields.</summary>
    /// <example><code>var value = record.Fields;</code></example>
    public IReadOnlyList<ProfilingDimension> Fields { get; init; } = [];

}

/// <summary>Projects HTTP source details without dependencies on the HTTP stack.</summary>
/// <example><code>var value = new HttpRequestProfilingMetadata();</code></example>
public sealed record HttpRequestProfilingMetadata
{
    /// <summary>Gets the lookup-only application request identifier.</summary>
    /// <example><code>var value = record.ApplicationRequestId;</code></example>
    public string ApplicationRequestId { get; init; }

    /// <summary>Gets declared request length separately from observed bytes.</summary>
    /// <example><code>var value = record.DeclaredRequestBytes;</code></example>
    public long? DeclaredRequestBytes { get; init; }

    /// <summary>Gets declared response length separately from observed bytes.</summary>
    /// <example><code>var value = record.DeclaredResponseBytes;</code></example>
    public long? DeclaredResponseBytes { get; init; }

    /// <summary>Gets sampling-selected request concurrency before admission.</summary>
    /// <example><code>var value = record.ActiveSelectedRequestsAtEntry;</code></example>
    public int? ActiveSelectedRequestsAtEntry { get; init; }

    /// <summary>Gets the immutable strategy selected at entry.</summary>
    /// <example><code>var value = record.SamplingStrategyKey;</code></example>
    public string SamplingStrategyKey { get; init; }

    /// <summary>Gets the immutable effective policy identifier.</summary>
    /// <example><code>var value = record.SamplingConfigurationKey;</code></example>
    public string SamplingConfigurationKey { get; init; }

    /// <summary>Gets known selection probability, without implying persistence probability.</summary>
    /// <example><code>var value = record.SamplingInclusionProbability;</code></example>
    public double? SamplingInclusionProbability { get; init; }

    /// <summary>Gets whether the path key uses a bounded prefix and stable hash.</summary>
    /// <example><code>var value = record.PathKeyShortened;</code></example>
    public bool PathKeyShortened { get; init; }

    /// <summary>Gets the HTTP method.</summary>
    /// <example><code>var value = record.Method;</code></example>
    public string Method { get; init; }

    /// <summary>Gets the incoming path without query string.</summary>
    /// <example><code>var value = record.Path;</code></example>
    public string Path { get; init; }

    /// <summary>Gets the matched endpoint route template.</summary>
    /// <example><code>var value = record.Route;</code></example>
    public string Route { get; init; }

    /// <summary>Gets the terminal HTTP status.</summary>
    /// <example><code>var value = record.StatusCode;</code></example>
    public int? StatusCode { get; init; }

    /// <summary>Gets observed request-body bytes when enabled.</summary>
    /// <example><code>var value = record.RequestBytes;</code></example>
    public long? RequestBytes { get; init; }

    /// <summary>Gets observed transmitted response bytes.</summary>
    /// <example><code>var value = record.ResponseBytes;</code></example>
    public long? ResponseBytes { get; init; }

    /// <summary>Gets request byte observation quality.</summary>
    /// <example><code>var value = record.RequestBytesQuality;</code></example>
    public ProfilingObservationQuality RequestBytesQuality { get; init; } = ProfilingObservationQuality.Unavailable;

    /// <summary>Gets response byte observation quality.</summary>
    /// <example><code>var value = record.ResponseBytesQuality;</code></example>
    public ProfilingObservationQuality ResponseBytesQuality { get; init; } = ProfilingObservationQuality.Unavailable;

    /// <summary>Gets evidence of transport interruption.</summary>
    /// <example><code>var value = record.TransportAborted;</code></example>
    public bool TransportAborted { get; init; }

    /// <summary>Gets whether an exception handler observed a failure.</summary>
    /// <example><code>var value = record.HandledException;</code></example>
    public bool HandledException { get; init; }

}

/// <summary>Retains additive invocation statistics for one outcome.</summary>
/// <example><code>var value = new ProfilingDurationStatistics();</code></example>
public sealed record ProfilingDurationStatistics
{
    /// <summary>Gets whether cumulative duration arithmetic exceeded its representation.</summary>
    /// <example><code>if (statistics.Unavailable) ShowUnavailable();</code></example>
    public bool Unavailable { get; init; }

    /// <summary>Gets contributing invocation count.</summary>
    /// <example><code>var value = record.Count;</code></example>
    public long Count { get; init; }

    /// <summary>Gets cumulative inclusive duration.</summary>
    /// <example><code>var value = record.TotalDuration;</code></example>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Gets cumulative time outside direct children.</summary>
    /// <example><code>var value = record.TotalSelfDuration;</code></example>
    public TimeSpan TotalSelfDuration { get; init; }

    /// <summary>Gets the shortest invocation.</summary>
    /// <example><code>var value = record.MinimumDuration;</code></example>
    public TimeSpan MinimumDuration { get; init; }

    /// <summary>Gets the longest invocation.</summary>
    /// <example><code>var value = record.MaximumDuration;</code></example>
    public TimeSpan MaximumDuration { get; init; }

}

/// <summary>Keeps timing statistics meaningful under outcome filters.</summary>
/// <example><code>var value = new ProfilingSegmentOutcomeSummary();</code></example>
public sealed record ProfilingSegmentOutcomeSummary
{
    /// <summary>Gets the invocation outcome.</summary>
    /// <example><code>var value = record.Outcome;</code></example>
    public ProfilingSegmentOutcome Outcome { get; init; }

    /// <summary>Gets outcome-specific duration statistics.</summary>
    /// <example><code>var value = record.Statistics;</code></example>
    public ProfilingDurationStatistics Statistics { get; init; } = new();

}

/// <summary>Retains reducer state, contributing counts and completion ordering.</summary>
/// <example><code>var value = new ProfilingMeasurementSummary();</code></example>
public sealed record ProfilingMeasurementSummary
{
    /// <summary>Gets the measurement name.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the case-sensitive unit.</summary>
    /// <example><code>var value = record.Unit;</code></example>
    public string Unit { get; init; }

    /// <summary>Gets the declared reducer.</summary>
    /// <example><code>var value = record.Aggregation;</code></example>
    public MeasurementAggregation Aggregation { get; init; }

    /// <summary>Gets the reduced value, absent when unavailable.</summary>
    /// <example><code>var value = record.Value;</code></example>
    public ProfilingValue Value { get; init; }

    /// <summary>Gets retained sum for weighted averages.</summary>
    /// <example><code>var value = record.Sum;</code></example>
    public ProfilingValue Sum { get; init; }

    /// <summary>Gets contributing sample count.</summary>
    /// <example><code>var value = record.SampleCount;</code></example>
    public long SampleCount { get; init; }

    /// <summary>Gets incompatible unit or reducer evidence.</summary>
    /// <example><code>var value = record.Conflicting;</code></example>
    public bool Conflicting { get; init; }

    /// <summary>Gets arithmetic overflow or invalid aggregate evidence.</summary>
    /// <example><code>var value = record.Unavailable;</code></example>
    public bool Unavailable { get; init; }

    /// <summary>Gets segment-local completion ordering.</summary>
    /// <example><code>var value = record.LastCompletionSequence;</code></example>
    public long LastCompletionSequence { get; init; }

    /// <summary>Gets observed completion UTC for Last.</summary>
    /// <example><code>var value = record.LastCompletedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset LastCompletedUtc { get; init; }

    /// <summary>Gets outcome-specific reducers.</summary>
    /// <example><code>var value = record.Outcomes;</code></example>
    public IReadOnlyList<ProfilingOutcomeMeasurementSummary> Outcomes { get; init; } = [];

}

/// <summary>Retains one measurement reducer for an invocation outcome.</summary>
/// <example><code>var value = new ProfilingOutcomeMeasurementSummary();</code></example>
public sealed record ProfilingOutcomeMeasurementSummary
{
    /// <summary>Gets the outcome.</summary>
    /// <example><code>var value = record.Outcome;</code></example>
    public ProfilingSegmentOutcome Outcome { get; init; }

    /// <summary>Gets the reduced value.</summary>
    /// <example><code>var value = record.Value;</code></example>
    public ProfilingValue Value { get; init; }

    /// <summary>Gets the weighted-average sum.</summary>
    /// <example><code>var value = record.Sum;</code></example>
    public ProfilingValue Sum { get; init; }

    /// <summary>Gets contributing samples.</summary>
    /// <example><code>var value = record.SampleCount;</code></example>
    public long SampleCount { get; init; }

    /// <summary>Gets the operation-local completion order.</summary>
    /// <example><code>var value = record.LastCompletionSequence;</code></example>
    public long LastCompletionSequence { get; init; }

    /// <summary>Gets observed last completion UTC.</summary>
    /// <example><code>var value = record.LastCompletedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset LastCompletedUtc { get; init; }

    /// <summary>Gets arithmetic unavailability.</summary>
    /// <example><code>var value = record.Unavailable;</code></example>
    public bool Unavailable { get; init; }

}

/// <summary>Exposes consistent, mixed and missing invocation metadata.</summary>
/// <example><code>var value = new ProfilingSegmentDimensionSummary();</code></example>
public sealed record ProfilingSegmentDimensionSummary
{
    /// <summary>Gets the dimension name.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the first admitted value.</summary>
    /// <example><code>var value = record.Value;</code></example>
    public ProfilingValue Value { get; init; }

    /// <summary>Gets invocations supplying the dimension.</summary>
    /// <example><code>var value = record.SampleCount;</code></example>
    public long SampleCount { get; init; }

    /// <summary>Gets differing values across invocations.</summary>
    /// <example><code>var value = record.Mixed;</code></example>
    public bool Mixed { get; init; }

    /// <summary>Gets a missing value in some invocations.</summary>
    /// <example><code>var value = record.Partial;</code></example>
    public bool Partial { get; init; }

}

/// <summary>Aggregates classified failures with bounded category cardinality.</summary>
/// <example><code>var value = new ProfilingFailureSummary();</code></example>
public sealed record ProfilingFailureSummary
{
    /// <summary>Gets one safe category and optional sample.</summary>
    /// <example><code>var value = record.Failure;</code></example>
    public ProfilingFailureDescriptor Failure { get; init; }

    /// <summary>Gets failed invocation count in this category.</summary>
    /// <example><code>var value = record.Count;</code></example>
    public long Count { get; init; }

    /// <summary>Gets first observed failure UTC.</summary>
    /// <example><code>var value = record.FirstObservedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset FirstObservedUtc { get; init; }

    /// <summary>Gets last observed failure UTC.</summary>
    /// <example><code>var value = record.LastObservedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset LastObservedUtc { get; init; }

}

/// <summary>Aggregates one complete structured path without retaining occurrence traces.</summary>
/// <example><code>var value = new ProfilingSegmentSummary();</code></example>
public sealed record ProfilingSegmentSummary
{
    /// <summary>Gets the owning root ID.</summary>
    /// <example><code>var value = record.OperationId;</code></example>
    public Guid OperationId { get; init; }

    /// <summary>Gets the executing process ID.</summary>
    /// <example><code>var value = record.NodeId;</code></example>
    public Guid NodeId { get; init; }

    /// <summary>Gets the first admitted leaf label.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets an optional first display name.</summary>
    /// <example><code>var value = record.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Gets structured ancestry.</summary>
    /// <example><code>var value = record.Path;</code></example>
    public ProfilingSegmentPath Path { get; init; }

    /// <summary>Gets structured parent ancestry when nested.</summary>
    /// <example><code>var value = record.ParentPath;</code></example>
    public ProfilingSegmentPath ParentPath { get; init; }

    /// <summary>Gets all admitted invocation statistics.</summary>
    /// <example><code>var value = record.Statistics;</code></example>
    public ProfilingDurationStatistics Statistics { get; init; } = new();

    /// <summary>Gets per-outcome timing.</summary>
    /// <example><code>var value = record.Outcomes;</code></example>
    public IReadOnlyList<ProfilingSegmentOutcomeSummary> Outcomes { get; init; } = [];

    /// <summary>Gets bounded metadata consistency.</summary>
    /// <example><code>var value = record.Dimensions;</code></example>
    public IReadOnlyList<ProfilingSegmentDimensionSummary> Dimensions { get; init; } = [];

    /// <summary>Gets reduced measurements.</summary>
    /// <example><code>var value = record.Measurements;</code></example>
    public IReadOnlyList<ProfilingMeasurementSummary> Measurements { get; init; } = [];

    /// <summary>Gets bounded failure categories.</summary>
    /// <example><code>var value = record.Failures;</code></example>
    public IReadOnlyList<ProfilingFailureSummary> Failures { get; init; } = [];

    /// <summary>Gets categories beyond the configured cap.</summary>
    /// <example><code>var value = record.OtherFailureCount;</code></example>
    public long OtherFailureCount { get; init; }

    /// <summary>Gets incomplete instrumentation evidence.</summary>
    /// <example><code>var value = record.PartialCoverage;</code></example>
    public bool PartialCoverage { get; init; }

}

/// <summary>Counts root time once by online top-level coverage.</summary>
/// <example><code>var value = new ProfilingWallTimeBucket();</code></example>
public sealed record ProfilingWallTimeBucket
{
    /// <summary>Gets Segment, Parallel or OutsideSegments classification.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public ProfilingWallTimeKind Kind { get; init; }

    /// <summary>Gets a top-level segment key only for Segment buckets.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets exclusive root wall time in this bucket.</summary>
    /// <example><code>var value = record.Duration;</code></example>
    public TimeSpan Duration { get; init; }

}

/// <summary>Freezes execution diagnostics, typed metadata and segment aggregates.</summary>
/// <example><code>var value = new OperationProfilingRecord();</code></example>
public sealed record OperationProfilingRecord
{
    /// <summary>Gets the unique operation occurrence ID.</summary>
    /// <example><code>var value = record.Id;</code></example>
    public Guid Id { get; init; }

    /// <summary>Gets the logical grouping key.</summary>
    /// <example><code>var value = record.Key;</code></example>
    public string Key { get; init; }

    /// <summary>Gets the execution kind.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Gets the optional display label.</summary>
    /// <example><code>var value = record.DisplayName;</code></example>
    public string DisplayName { get; init; }

    /// <summary>Gets the lookup correlation ID.</summary>
    /// <example><code>var value = record.CorrelationId;</code></example>
    public string CorrelationId { get; init; }

    /// <summary>Gets a non-owning parent link.</summary>
    /// <example><code>var value = record.ParentOperationId;</code></example>
    public Guid? ParentOperationId { get; init; }

    /// <summary>Gets cached process identity and display metadata.</summary>
    /// <example><code>var value = record.Node;</code></example>
    public ProfilingNode Node { get; init; }

    /// <summary>Gets the executing process identity for filtering and Runtime correlation.</summary>
    /// <example><code>var id = record.NodeId;</code></example>
    public Guid NodeId => this.Node?.Identity.Id ?? Guid.Empty;

    /// <summary>Gets the readable executing-node key.</summary>
    /// <example><code>var key = record.NodeKey;</code></example>
    public string NodeKey => this.Node?.Identity.Key;

    /// <summary>Gets observed execution start UTC.</summary>
    /// <example><code>var value = record.StartedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets observation end UTC.</summary>
    /// <example><code>var value = record.CompletedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset CompletedUtc { get; init; }

    /// <summary>Gets monotonic elapsed duration.</summary>
    /// <example><code>var value = record.Duration;</code></example>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets the process monotonic start stamp.</summary>
    /// <example><code>var value = record.StartedTimestamp;</code></example>
    public long StartedTimestamp { get; init; }

    /// <summary>Gets the process monotonic end stamp.</summary>
    /// <example><code>var value = record.CompletedTimestamp;</code></example>
    public long CompletedTimestamp { get; init; }

    /// <summary>Gets monotonic ticks per second.</summary>
    /// <example><code>var value = record.TimestampFrequency;</code></example>
    public long TimestampFrequency { get; init; }

    /// <summary>Gets the observed root outcome.</summary>
    /// <example><code>var value = record.Outcome;</code></example>
    public OperationProfilingOutcome Outcome { get; init; }

    /// <summary>Gets bounded safe root failure evidence.</summary>
    /// <example><code>var value = record.Failure;</code></example>
    public ProfilingFailureDescriptor Failure { get; init; }

    /// <summary>Gets admitted recording concurrency including this root.</summary>
    /// <example><code>var value = record.ActiveRecordingOperationsAtEntry;</code></example>
    public int ActiveRecordingOperationsAtEntry { get; init; }

    /// <summary>Gets a non-owning optional Runtime hint.</summary>
    /// <example><code>var value = record.RuntimeSessionKeyAtStart;</code></example>
    public string RuntimeSessionKeyAtStart { get; init; }

    /// <summary>Gets a non-owning optional Runtime session identity at entry.</summary>
    /// <example><code>var id = record.RuntimeSessionIdAtStart;</code></example>
    public Guid? RuntimeSessionIdAtStart { get; init; }

    /// <summary>Gets root comparison metadata.</summary>
    /// <example><code>var value = record.Dimensions;</code></example>
    public IReadOnlyList<ProfilingDimension> Dimensions { get; init; } = [];

    /// <summary>Gets root measurements without implicit addition.</summary>
    /// <example><code>var value = record.Measurements;</code></example>
    public IReadOnlyList<ProfilingMeasurement> Measurements { get; init; } = [];

    /// <summary>Gets bounded source envelopes.</summary>
    /// <example><code>var value = record.Sources;</code></example>
    public IReadOnlyList<ProfilingAdapterMetadata> Sources { get; init; } = [];

    /// <summary>Gets an optional pure HTTP projection.</summary>
    /// <example><code>var value = record.Http;</code></example>
    public HttpRequestProfilingMetadata Http { get; init; }

    /// <summary>Gets independent capture limitations.</summary>
    /// <example><code>var value = record.Quality;</code></example>
    public ProfilingCaptureQuality Quality { get; init; } = new();

    /// <summary>Gets full-path aggregate summaries.</summary>
    /// <example><code>var value = record.Segments;</code></example>
    public IReadOnlyList<ProfilingSegmentSummary> Segments { get; init; } = [];

    /// <summary>Gets mutually exclusive root coverage.</summary>
    /// <example><code>var value = record.WallTime;</code></example>
    public IReadOnlyList<ProfilingWallTimeBucket> WallTime { get; init; } = [];

    /// <summary>Gets accounted retained payload size.</summary>
    /// <example><code>var value = record.EstimatedPayloadBytes;</code></example>
    public long EstimatedPayloadBytes { get; init; }

}
