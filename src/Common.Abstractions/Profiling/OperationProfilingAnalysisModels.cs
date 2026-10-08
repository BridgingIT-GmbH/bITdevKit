// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Contains invocation-weighted statistics and distributions of one segment path per owning operation.</summary>
/// <example><code>var value = new OperationProfilingSegmentAnalysis();</code></example>
public sealed record OperationProfilingSegmentAnalysis
{
    /// <summary>Gets the owning operation grouping key.</summary>
    /// <example><code>var value = record.OperationKey;</code></example>
    public string OperationKey { get; init; }

    /// <summary>Gets the owning operation kind.</summary>
    /// <example><code>var value = record.Kind;</code></example>
    public string Kind { get; init; }

    /// <summary>Gets the complete segment grouping path.</summary>
    /// <example><code>var value = record.Path;</code></example>
    public ProfilingSegmentPath Path { get; init; }

    /// <summary>Gets the number of owners with selected invocations.</summary>
    /// <example><code>var value = record.OperationCount;</code></example>
    public long OperationCount { get; init; }

    /// <summary>Gets the summed selected invocation count, or zero when unavailable.</summary>
    /// <example><code>var value = record.InvocationCount;</code></example>
    public long InvocationCount { get; init; }

    /// <summary>Gets whether an overflow or unavailable timing prevents an exact invocation aggregate.</summary>
    /// <example><code>var value = record.Unavailable;</code></example>
    public bool Unavailable { get; init; }

    /// <summary>Gets whether at least one owner has incomplete segment coverage.</summary>
    /// <example><code>var value = record.PartialCoverage;</code></example>
    public bool PartialCoverage { get; init; }

    /// <summary>Gets summed duration divided by summed invocations.</summary>
    /// <example><code>var value = record.InvocationMeanMilliseconds;</code></example>
    public double InvocationMeanMilliseconds { get; init; }

    /// <summary>Gets summed self duration divided by summed invocations.</summary>
    /// <example><code>var value = record.InvocationSelfMeanMilliseconds;</code></example>
    public double InvocationSelfMeanMilliseconds { get; init; }

    /// <summary>Gets the minimum selected invocation duration.</summary>
    /// <example><code>var value = record.MinimumInvocationMilliseconds;</code></example>
    public double MinimumInvocationMilliseconds { get; init; }

    /// <summary>Gets the maximum selected invocation duration.</summary>
    /// <example><code>var value = record.MaximumInvocationMilliseconds;</code></example>
    public double MaximumInvocationMilliseconds { get; init; }

    /// <summary>Gets the distribution of total duration per owner; these are not invocation percentiles.</summary>
    /// <example><code>var value = record.TotalPerOperation;</code></example>
    public OperationProfilingDistribution TotalPerOperation { get; init; }

    /// <summary>Gets the distribution of self duration per owner.</summary>
    /// <example><code>var value = record.SelfPerOperation;</code></example>
    public OperationProfilingDistribution SelfPerOperation { get; init; }

    /// <summary>Gets constant, mixed and partial metadata across selected owners.</summary>
    /// <example><code>var value = record.Dimensions;</code></example>
    public IReadOnlyList<ProfilingSegmentDimensionSummary> Dimensions { get; init; } = [];

    /// <summary>Gets measurements reduced only within compatible units and reducers.</summary>
    /// <example><code>var value = record.Measurements;</code></example>
    public IReadOnlyList<ProfilingMeasurementSummary> Measurements { get; init; } = [];

    /// <summary>Gets observed invocation counts by outcome.</summary>
    /// <example><code>var value = record.Outcomes;</code></example>
    public IReadOnlyDictionary<ProfilingSegmentOutcome, long> Outcomes { get; init; } = new Dictionary<ProfilingSegmentOutcome, long>();

}

/// <summary>Labels retained observations for one HTTP sampling policy without extrapolating execution totals.</summary>
/// <example><code>var value = new OperationProfilingSamplingSummary();</code></example>
public sealed record OperationProfilingSamplingSummary
{
    /// <summary>Gets the strategy identifier; null identifies non-HTTP work.</summary>
    /// <example><code>var value = record.StrategyKey;</code></example>
    public string StrategyKey { get; init; }

    /// <summary>Gets the policy configuration identifier.</summary>
    /// <example><code>var value = record.ConfigurationKey;</code></example>
    public string ConfigurationKey { get; init; }

    /// <summary>Gets the retained selected operation count.</summary>
    /// <example><code>var value = record.Count;</code></example>
    public long Count { get; init; }

    /// <summary>Gets the lowest known inclusion probability.</summary>
    /// <example><code>var value = record.MinimumInclusionProbability;</code></example>
    public double? MinimumInclusionProbability { get; init; }

    /// <summary>Gets the highest known inclusion probability.</summary>
    /// <example><code>var value = record.MaximumInclusionProbability;</code></example>
    public double? MaximumInclusionProbability { get; init; }

    /// <summary>Gets whether any selected probability is unknown.</summary>
    /// <example><code>var value = record.ProbabilityUnavailable;</code></example>
    public bool ProbabilityUnavailable { get; init; }

}

/// <summary>Selects bounded Runtime evidence for an exact process identity and UTC observation interval.</summary>
/// <example><code>var value = new RuntimeProfilingCorrelationRequest();</code></example>
public sealed record RuntimeProfilingCorrelationRequest
{
    /// <summary>Gets the complete process descriptor, including identity and actual process start.</summary>
    /// <example><code>var value = record.Node;</code></example>
    public ProfilingNode Node { get; init; }

    /// <summary>Gets the inclusive operation start in UTC.</summary>
    /// <example><code>var value = record.FromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset FromUtc { get; init; }

    /// <summary>Gets the exclusive operation end, or the same instant for zero-duration work.</summary>
    /// <example><code>var value = record.ToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ToUtc { get; init; }

    /// <summary>Gets the limit including explicitly labeled surrounding samples.</summary>
    /// <example><code>var value = record.MaximumSnapshots;</code></example>
    public int MaximumSnapshots { get; init; } = 2000;

}

/// <summary>Returns bounded provider evidence from matching collection windows; it owns no operation lifecycle.</summary>
/// <example><code>var value = new RuntimeProfilingCorrelationSelection();</code></example>
public sealed record RuntimeProfilingCorrelationSelection
{
    /// <summary>Gets interval samples and nearest surrounding samples.</summary>
    /// <example><code>var value = record.Snapshots;</code></example>
    public IReadOnlyList<RuntimeProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets sessions with matching actual node collection windows.</summary>
    /// <example><code>var value = record.Sessions;</code></example>
    public IReadOnlyList<RuntimeProfilingSession> Sessions { get; init; } = [];

    /// <summary>Gets the matching node collection intervals.</summary>
    /// <example><code>var value = record.Participations;</code></example>
    public IReadOnlyList<RuntimeProfilingNodeParticipation> Participations { get; init; } = [];

}

/// <summary>Optional provider facet for bounded, server-filtered Runtime correlation.</summary>
/// <example><code>var result = await store.QueryCorrelationAsync(request, token);</code></example>
public interface IRuntimeProfilingCorrelationStore
{
    /// <summary>Selects evidence by exact process identity and observed intervals, returning a limit error instead of truncating.</summary>
    /// <example><code>var result = await store.QueryCorrelationAsync(request, token);</code></example>
    Task<IResult<RuntimeProfilingCorrelationSelection>> QueryCorrelationAsync(RuntimeProfilingCorrelationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Labels a snapshot and the actual interval represented by its process-level rate metrics.</summary>
/// <example><code>var value = new OperationProfilingRuntimeSample();</code></example>
public sealed record OperationProfilingRuntimeSample
{
    /// <summary>Gets the unchanged Runtime observation.</summary>
    /// <example><code>var value = record.Snapshot;</code></example>
    public RuntimeProfilingSnapshot Snapshot { get; init; }

    /// <summary>Gets Inside, Before, or After relative to the operation interval.</summary>
    /// <example><code>var value = record.Relation;</code></example>
    public string Relation { get; init; }

    /// <summary>Gets the earlier sample timestamp used by interval metrics, when known.</summary>
    /// <example><code>var value = record.MetricIntervalFromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? MetricIntervalFromUtc { get; init; }

    /// <summary>Gets the current sample timestamp for interval metrics, when known.</summary>
    /// <example><code>var value = record.MetricIntervalToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset? MetricIntervalToUtc { get; init; }

    /// <summary>Gets whether sequence, skipped observations or cadence show a gap.</summary>
    /// <example><code>var value = record.SamplingGap;</code></example>
    public bool SamplingGap { get; init; }

    /// <summary>Gets whether UTC and monotonic sample order disagree.</summary>
    /// <example><code>var value = record.ClockDiscontinuity;</code></example>
    public bool ClockDiscontinuity { get; init; }

}

/// <summary>Describes missing Runtime sampling evidence without interpolating across it.</summary>
/// <example><code>var value = new OperationProfilingRuntimeGap();</code></example>
public sealed record OperationProfilingRuntimeGap
{
    /// <summary>Gets the collection session key.</summary>
    /// <example><code>var value = record.SessionKey;</code></example>
    public string SessionKey { get; init; }

    /// <summary>Gets the earlier observed UTC timestamp.</summary>
    /// <example><code>var value = record.FromUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset FromUtc { get; init; }

    /// <summary>Gets the later observed UTC timestamp.</summary>
    /// <example><code>var value = record.ToUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ToUtc { get; init; }

    /// <summary>Gets a safe coverage limitation code.</summary>
    /// <example><code>var value = record.Reason;</code></example>
    public string Reason { get; init; }

}
