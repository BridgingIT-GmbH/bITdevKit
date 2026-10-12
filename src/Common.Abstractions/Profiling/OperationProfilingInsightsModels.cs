// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by the LICENSE file.

namespace BridgingIT.DevKit.Common;

/// <summary>Provides exact hotspot analysis of one bounded retained selection.</summary>
/// <example><code>var hotspots = await queries.InsightsAsync(new() { FromUtc = from, ToUtc = to });</code></example>
public sealed record OperationProfilingInsights
{
    /// <summary>Gets the selection's publication boundary.</summary>
    /// <example><code>var boundary = insights.Boundary;</code></example>
    public ProfilingQueryBoundary Boundary { get; init; }
    /// <summary>Gets matching retained executions, including every selected outcome.</summary>
    /// <example><code>var count = insights.Count;</code></example>
    public long Count { get; init; }
    /// <summary>Gets retained root counts by outcome.</summary>
    /// <example><code>var failures = insights.Outcomes.GetValueOrDefault(OperationProfilingOutcome.Failed);</code></example>
    public IReadOnlyDictionary<OperationProfilingOutcome, long> Outcomes { get; init; } = new Dictionary<OperationProfilingOutcome, long>();
    /// <summary>Gets executions with incomplete or partial capture evidence.</summary>
    /// <example><code>var partial = insights.PartialCount;</code></example>
    public long PartialCount { get; init; }
    /// <summary>Gets exact groups before dashboard ranking and row limits.</summary>
    /// <example><code>var groups = insights.Groups;</code></example>
    public IReadOnlyList<OperationProfilingInsightGroup> Groups { get; init; } = [];
}

/// <summary>Separates completed latency and segment costs from root outcome counts for one logical operation and HTTP method.</summary>
/// <example><code>var p95 = group.CompletedLatency.P95Milliseconds;</code></example>
public sealed record OperationProfilingInsightGroup
{
    /// <summary>Gets the operation grouping key.</summary>
    /// <example><code>var key = group.Key;</code></example>
    public string Key { get; init; }
    /// <summary>Gets the operation kind.</summary>
    /// <example><code>var kind = group.Kind;</code></example>
    public string Kind { get; init; }
    /// <summary>Gets the HTTP method, or null for other operations.</summary>
    /// <example><code>var method = group.HttpMethod;</code></example>
    public string HttpMethod { get; init; }
    /// <summary>Gets every matching retained execution in this group.</summary>
    /// <example><code>var count = group.Count;</code></example>
    public long Count { get; init; }
    /// <summary>Gets root counts by outcome; failure rates use Count as the denominator.</summary>
    /// <example><code>var failures = group.Outcomes.GetValueOrDefault(OperationProfilingOutcome.Failed);</code></example>
    public IReadOnlyDictionary<OperationProfilingOutcome, long> Outcomes { get; init; } = new Dictionary<OperationProfilingOutcome, long>();
    /// <summary>Gets valid completed latency samples; incomplete observations never contribute.</summary>
    /// <example><code>var samples = group.CompletedLatency.Count;</code></example>
    public OperationProfilingDistribution CompletedLatency { get; init; } = new();
    /// <summary>Gets accumulated completed elapsed milliseconds, without attributing CPU or adding child segment totals.</summary>
    /// <example><code>var elapsed = group.CompletedElapsedMilliseconds;</code></example>
    public double CompletedElapsedMilliseconds { get; init; }
    /// <summary>Gets roots whose capture evidence is partial or incomplete.</summary>
    /// <example><code>var partial = group.PartialCount;</code></example>
    public long PartialCount { get; init; }
    /// <summary>Gets roots with at least one failed segment, independently of root outcome.</summary>
    /// <example><code>var failures = group.SegmentFailureCount;</code></example>
    public long SegmentFailureCount { get; init; }
    /// <summary>Gets a real slowest completed occurrence, or a real occurrence when no completed latency is available.</summary>
    /// <example><code>var id = group.RepresentativeId;</code></example>
    public Guid RepresentativeId { get; init; }
    /// <summary>Gets aggregate segments from completed latency owners, with self time, repetition and coverage.</summary>
    /// <example><code>var segments = group.Segments;</code></example>
    public IReadOnlyList<OperationProfilingSegmentAnalysis> Segments { get; init; } = [];
}
