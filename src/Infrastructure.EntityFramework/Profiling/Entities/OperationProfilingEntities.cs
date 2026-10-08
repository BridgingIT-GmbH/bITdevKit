// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Persists one immutable operation graph with portable indexed query projections.</summary>
/// <example><code>var entity = new OperationProfilingEntity();</code></example>
public sealed class OperationProfilingEntity
{
    /// <summary>Gets or sets the immutable operation occurrence identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the portable ordinal GUID sorting representation.</summary>
    /// <example><code>var value = entity.CanonicalIdBytes;</code></example>
    public byte[] CanonicalIdBytes { get; set; }

    /// <summary>Gets or sets the shared cached process owner.</summary>
    /// <example><code>var value = entity.NodeId;</code></example>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the immutable readable node key for one-read graph reconstruction.</summary>
    /// <example><code>var value = entity.NodeKey;</code></example>
    public string NodeKey { get; set; }

    /// <summary>Gets or sets the non-cascading shared process descriptor.</summary>
    /// <example><code>var value = entity.Node;</code></example>
    public ProfilingNodeEntity Node { get; set; }

    /// <summary>Gets or sets the original writer identity retained independently of lease cleanup.</summary>
    /// <example><code>var value = entity.WriterId;</code></example>
    public Guid WriterId { get; set; }

    /// <summary>Gets or sets the original writer-local completion number.</summary>
    /// <example><code>var value = entity.CompletionSequence;</code></example>
    public long CompletionSequence { get; set; }

    /// <summary>Gets or sets the original registration generation.</summary>
    /// <example><code>var value = entity.WriterGeneration;</code></example>
    public long WriterGeneration { get; set; }

    /// <summary>Gets or sets the transactionally visible publication position.</summary>
    /// <example><code>var value = entity.CommitWatermark;</code></example>
    public long CommitWatermark { get; set; }

    /// <summary>Gets or sets the supplied operation display key.</summary>
    /// <example><code>var value = entity.Key;</code></example>
    public string Key { get; set; }

    /// <summary>Gets or sets the supplied operation kind.</summary>
    /// <example><code>var value = entity.Kind;</code></example>
    public string Kind { get; set; }

    /// <summary>Gets or sets the complete canonical key comparison representation.</summary>
    /// <example><code>var value = entity.KeyBytes;</code></example>
    public byte[] KeyBytes { get; set; }

    /// <summary>Gets or sets the indexed canonical key digest with full-value collision verification.</summary>
    /// <example><code>var value = entity.KeyHash;</code></example>
    public byte[] KeyHash { get; set; }

    /// <summary>Gets or sets the complete canonical kind comparison representation.</summary>
    /// <example><code>var value = entity.KindBytes;</code></example>
    public byte[] KindBytes { get; set; }

    /// <summary>Gets or sets the indexed canonical kind digest with full-value collision verification.</summary>
    /// <example><code>var value = entity.KindHash;</code></example>
    public byte[] KindHash { get; set; }

    /// <summary>Gets or sets the length-prefixed kind and key grouping representation.</summary>
    /// <example><code>var value = entity.BaseGroupBytes;</code></example>
    public byte[] BaseGroupBytes { get; set; }

    /// <summary>Gets or sets the observed UTC start ticks.</summary>
    /// <example><code>var value = entity.StartedUtcTicks;</code></example>
    public long StartedUtcTicks { get; set; }

    /// <summary>Gets or sets the observed UTC completion ticks.</summary>
    /// <example><code>var value = entity.CompletedUtcTicks;</code></example>
    public long CompletedUtcTicks { get; set; }

    /// <summary>Gets or sets the monotonic elapsed ticks.</summary>
    /// <example><code>var value = entity.DurationTicks;</code></example>
    public long DurationTicks { get; set; }

    /// <summary>Gets or sets the observed business outcome.</summary>
    /// <example><code>var value = entity.Outcome;</code></example>
    public OperationProfilingOutcome Outcome { get; set; }

    /// <summary>Gets or sets the non-owning parent occurrence link.</summary>
    /// <example><code>var value = entity.ParentOperationId;</code></example>
    public Guid? ParentOperationId { get; set; }

    /// <summary>Gets or sets the exact lookup-only correlation value.</summary>
    /// <example><code>var value = entity.CorrelationBytes;</code></example>
    public byte[] CorrelationBytes { get; set; }

    /// <summary>Gets or sets the exact cached application version.</summary>
    /// <example><code>var value = entity.ApplicationVersionBytes;</code></example>
    public byte[] ApplicationVersionBytes { get; set; }

    /// <summary>Gets or sets whether any segment contains an observed failed invocation.</summary>
    /// <example><code>var value = entity.HasSegmentFailures;</code></example>
    public bool HasSegmentFailures { get; set; }

    /// <summary>Gets or sets the canonical HTTP method projection.</summary>
    /// <example><code>var value = entity.HttpMethodBytes;</code></example>
    public byte[] HttpMethodBytes { get; set; }

    /// <summary>Gets or sets the length-prefixed HTTP method or missing grouping component.</summary>
    /// <example><code>var value = entity.HttpMethodGroupBytes;</code></example>
    public byte[] HttpMethodGroupBytes { get; set; }

    /// <summary>Gets or sets the complete canonical route projection.</summary>
    /// <example><code>var value = entity.HttpRouteBytes;</code></example>
    public byte[] HttpRouteBytes { get; set; }

    /// <summary>Gets or sets the indexed route digest with collision verification.</summary>
    /// <example><code>var value = entity.HttpRouteHash;</code></example>
    public byte[] HttpRouteHash { get; set; }

    /// <summary>Gets or sets the final HTTP status when observed.</summary>
    /// <example><code>var value = entity.HttpStatusCode;</code></example>
    public int? HttpStatusCode { get; set; }

    /// <summary>Gets or sets the exact application request lookup identity.</summary>
    /// <example><code>var value = entity.ApplicationRequestIdBytes;</code></example>
    public byte[] ApplicationRequestIdBytes { get; set; }

    /// <summary>Gets or sets the canonical adapter sampling strategy.</summary>
    /// <example><code>var value = entity.SamplingStrategyBytes;</code></example>
    public byte[] SamplingStrategyBytes { get; set; }

    /// <summary>Gets or sets the canonical adapter sampling policy identifier.</summary>
    /// <example><code>var value = entity.SamplingConfigurationBytes;</code></example>
    public byte[] SamplingConfigurationBytes { get; set; }

    /// <summary>Gets or sets the conservatively charged frozen graph size.</summary>
    /// <example><code>var value = entity.EstimatedPayloadBytes;</code></example>
    public long EstimatedPayloadBytes { get; set; }

    /// <summary>Gets or sets the bounded frozen graph for one-read detail reconstruction.</summary>
    /// <example><code>var value = entity.RecordJson;</code></example>
    public string RecordJson { get; set; }

    /// <summary>Gets or sets the independently indexed typed metadata.</summary>
    /// <example><code>var value = entity.Dimensions;</code></example>
    public ICollection<OperationProfilingDimensionEntity> Dimensions { get; set; } = [];

    /// <summary>Gets or sets the independently addressed aggregate summary paths.</summary>
    /// <example><code>var value = entity.Segments;</code></example>
    public ICollection<OperationProfilingSegmentEntity> Segments { get; set; } = [];

    /// <summary>Gets or sets the independently stored root and aggregate measurements.</summary>
    /// <example><code>var value = entity.Measurements;</code></example>
    public ICollection<OperationProfilingMeasurementEntity> Measurements { get; set; } = [];
}

/// <summary>Persists one aggregate per complete operation segment path.</summary>
/// <example><code>var entity = new OperationProfilingSegmentEntity();</code></example>
public sealed class OperationProfilingSegmentEntity
{
    /// <summary>Gets or sets the internal aggregate row identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the owning immutable operation.</summary>
    /// <example><code>var value = entity.OperationId;</code></example>
    public Guid OperationId { get; set; }

    /// <summary>Gets or sets the root owning this complete summary.</summary>
    /// <example><code>var value = entity.Operation;</code></example>
    public OperationProfilingEntity Operation { get; set; }

    /// <summary>Gets or sets the cached process identity.</summary>
    /// <example><code>var value = entity.NodeId;</code></example>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the first admitted leaf label.</summary>
    /// <example><code>var value = entity.Key;</code></example>
    public string Key { get; set; }

    /// <summary>Gets or sets the canonical leaf comparison value.</summary>
    /// <example><code>var value = entity.KeyBytes;</code></example>
    public byte[] KeyBytes { get; set; }

    /// <summary>Gets or sets the indexed leaf comparison digest.</summary>
    /// <example><code>var value = entity.KeyHash;</code></example>
    public byte[] KeyHash { get; set; }

    /// <summary>Gets or sets the complete structured path comparison representation.</summary>
    /// <example><code>var value = entity.PathBytes;</code></example>
    public byte[] PathBytes { get; set; }

    /// <summary>Gets or sets the owner-qualified indexed path digest.</summary>
    /// <example><code>var value = entity.PathHash;</code></example>
    public byte[] PathHash { get; set; }

    /// <summary>Gets or sets the complete structured parent path.</summary>
    /// <example><code>var value = entity.ParentPathBytes;</code></example>
    public byte[] ParentPathBytes { get; set; }

    /// <summary>Gets or sets the indexed structured parent path digest.</summary>
    /// <example><code>var value = entity.ParentPathHash;</code></example>
    public byte[] ParentPathHash { get; set; }

    /// <summary>Gets or sets all admitted segment invocations.</summary>
    /// <example><code>var value = entity.Count;</code></example>
    public long Count { get; set; }

    /// <summary>Gets or sets inclusive invocation duration sum.</summary>
    /// <example><code>var value = entity.TotalDurationTicks;</code></example>
    public long TotalDurationTicks { get; set; }

    /// <summary>Gets or sets invocation duration outside the union of direct child work.</summary>
    /// <example><code>var value = entity.TotalSelfDurationTicks;</code></example>
    public long TotalSelfDurationTicks { get; set; }

    /// <summary>Gets or sets the minimum observed invocation duration.</summary>
    /// <example><code>var value = entity.MinimumDurationTicks;</code></example>
    public long MinimumDurationTicks { get; set; }

    /// <summary>Gets or sets the maximum observed invocation duration.</summary>
    /// <example><code>var value = entity.MaximumDurationTicks;</code></example>
    public long MaximumDurationTicks { get; set; }

    /// <summary>Gets or sets completed invocation observations.</summary>
    /// <example><code>var value = entity.CompletedCount;</code></example>
    public long CompletedCount { get; set; }

    /// <summary>Gets or sets failed invocation observations.</summary>
    /// <example><code>var value = entity.FailedCount;</code></example>
    public long FailedCount { get; set; }

    /// <summary>Gets or sets canceled invocation observations.</summary>
    /// <example><code>var value = entity.CanceledCount;</code></example>
    public long CanceledCount { get; set; }

    /// <summary>Gets or sets incomplete invocation observations.</summary>
    /// <example><code>var value = entity.IncompleteCount;</code></example>
    public long IncompleteCount { get; set; }

    /// <summary>Gets or sets arithmetic unavailability.</summary>
    /// <example><code>var value = entity.Unavailable;</code></example>
    public bool Unavailable { get; set; }

    /// <summary>Gets or sets the bounded aggregate outcomes, failure categories and reducer detail without occurrence traces.</summary>
    /// <example><code>var value = entity.SummaryJson;</code></example>
    public string SummaryJson { get; set; }
}

/// <summary>Persists one exact typed root or segment metadata projection.</summary>
/// <example><code>var entity = new OperationProfilingDimensionEntity();</code></example>
public sealed class OperationProfilingDimensionEntity
{
    /// <summary>Gets or sets the owned metadata row identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the root owning the typed value.</summary>
    /// <example><code>var value = entity.OperationId;</code></example>
    public Guid OperationId { get; set; }

    /// <summary>Gets or sets the root owner.</summary>
    /// <example><code>var value = entity.Operation;</code></example>
    public OperationProfilingEntity Operation { get; set; }

    /// <summary>Gets or sets the aggregate summary owner when this is segment metadata.</summary>
    /// <example><code>var value = entity.SegmentId;</code></example>
    public Guid? SegmentId { get; set; }

    /// <summary>Gets or sets the non-null root or segment scope digest for unique metadata ownership.</summary>
    /// <example><code>var value = entity.ScopeHash;</code></example>
    public byte[] ScopeHash { get; set; }

    /// <summary>Gets or sets the optional summary owner.</summary>
    /// <example><code>var value = entity.Segment;</code></example>
    public OperationProfilingSegmentEntity Segment { get; set; }

    /// <summary>Gets or sets the supplied metadata name.</summary>
    /// <example><code>var value = entity.Key;</code></example>
    public string Key { get; set; }

    /// <summary>Gets or sets the complete canonical metadata name.</summary>
    /// <example><code>var value = entity.KeyBytes;</code></example>
    public byte[] KeyBytes { get; set; }

    /// <summary>Gets or sets the indexed canonical metadata name digest.</summary>
    /// <example><code>var value = entity.KeyHash;</code></example>
    public byte[] KeyHash { get; set; }

    /// <summary>Gets or sets the exact scalar type or absent mixed-summary value.</summary>
    /// <example><code>var value = entity.ValueType;</code></example>
    public ProfilingValueType? ValueType { get; set; }

    /// <summary>Gets or sets the canonical lossless scalar representation.</summary>
    /// <example><code>var value = entity.ValueScalar;</code></example>
    public string ValueScalar { get; set; }

    /// <summary>Gets or sets the ordinal typed scalar comparison value.</summary>
    /// <example><code>var value = entity.ValueBytes;</code></example>
    public byte[] ValueBytes { get; set; }

    /// <summary>Gets or sets the length-prefixed typed value for portable grouping.</summary>
    /// <example><code>var value = entity.GroupPartBytes;</code></example>
    public byte[] GroupPartBytes { get; set; }

    /// <summary>Gets or sets contributing invocation observations.</summary>
    /// <example><code>var value = entity.SampleCount;</code></example>
    public long SampleCount { get; set; }

    /// <summary>Gets or sets whether multiple distinct invocation values were observed.</summary>
    /// <example><code>var value = entity.Mixed;</code></example>
    public bool Mixed { get; set; }

    /// <summary>Gets or sets whether the value was missing from some invocations.</summary>
    /// <example><code>var value = entity.Partial;</code></example>
    public bool Partial { get; set; }
}

/// <summary>Persists one root measurement or weighted aggregate reducer.</summary>
/// <example><code>var entity = new OperationProfilingMeasurementEntity();</code></example>
public sealed class OperationProfilingMeasurementEntity
{
    /// <summary>Gets or sets the owned measurement identity.</summary>
    /// <example><code>var value = entity.Id;</code></example>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the root owner identity.</summary>
    /// <example><code>var value = entity.OperationId;</code></example>
    public Guid OperationId { get; set; }

    /// <summary>Gets or sets the root owner.</summary>
    /// <example><code>var value = entity.Operation;</code></example>
    public OperationProfilingEntity Operation { get; set; }

    /// <summary>Gets or sets the summary owner identity when aggregated.</summary>
    /// <example><code>var value = entity.SegmentId;</code></example>
    public Guid? SegmentId { get; set; }

    /// <summary>Gets or sets the non-null root or segment scope digest for unique metadata ownership.</summary>
    /// <example><code>var value = entity.ScopeHash;</code></example>
    public byte[] ScopeHash { get; set; }

    /// <summary>Gets or sets the optional summary owner.</summary>
    /// <example><code>var value = entity.Segment;</code></example>
    public OperationProfilingSegmentEntity Segment { get; set; }

    /// <summary>Gets or sets the supplied measurement name.</summary>
    /// <example><code>var value = entity.Key;</code></example>
    public string Key { get; set; }

    /// <summary>Gets or sets the canonical measurement name.</summary>
    /// <example><code>var value = entity.KeyBytes;</code></example>
    public byte[] KeyBytes { get; set; }

    /// <summary>Gets or sets the indexed canonical measurement name digest.</summary>
    /// <example><code>var value = entity.KeyHash;</code></example>
    public byte[] KeyHash { get; set; }

    /// <summary>Gets or sets the case-sensitive unit representation.</summary>
    /// <example><code>var value = entity.UnitBytes;</code></example>
    public byte[] UnitBytes { get; set; }

    /// <summary>Gets or sets the declared invocation reducer.</summary>
    /// <example><code>var value = entity.Aggregation;</code></example>
    public MeasurementAggregation Aggregation { get; set; }

    /// <summary>Gets or sets the exact resulting numeric type.</summary>
    /// <example><code>var value = entity.ValueType;</code></example>
    public ProfilingValueType? ValueType { get; set; }

    /// <summary>Gets or sets the lossless resulting numeric representation.</summary>
    /// <example><code>var value = entity.ValueScalar;</code></example>
    public string ValueScalar { get; set; }

    /// <summary>Gets or sets the exact weighted-average sum type.</summary>
    /// <example><code>var value = entity.SumType;</code></example>
    public ProfilingValueType? SumType { get; set; }

    /// <summary>Gets or sets the lossless contributing sum.</summary>
    /// <example><code>var value = entity.SumScalar;</code></example>
    public string SumScalar { get; set; }

    /// <summary>Gets or sets the contributing observation count.</summary>
    /// <example><code>var value = entity.SampleCount;</code></example>
    public long SampleCount { get; set; }

    /// <summary>Gets or sets the operation-local observed last completion order.</summary>
    /// <example><code>var value = entity.LastCompletionSequence;</code></example>
    public long LastCompletionSequence { get; set; }

    /// <summary>Gets or sets the observed last contributing completion UTC.</summary>
    /// <example><code>var value = entity.LastCompletedUtcTicks;</code></example>
    public long LastCompletedUtcTicks { get; set; }

    /// <summary>Gets or sets whether compatible arithmetic is unavailable.</summary>
    /// <example><code>var value = entity.Unavailable;</code></example>
    public bool Unavailable { get; set; }

    /// <summary>Gets or sets whether units or reduction rules conflict.</summary>
    /// <example><code>var value = entity.Conflicting;</code></example>
    public bool Conflicting { get; set; }

    /// <summary>Gets or sets the bounded outcome-specific typed reducer summaries.</summary>
    /// <example><code>var value = entity.OutcomesJson;</code></example>
    public string OutcomesJson { get; set; }
}
