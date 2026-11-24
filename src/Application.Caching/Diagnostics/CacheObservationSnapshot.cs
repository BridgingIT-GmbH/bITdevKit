// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports bounded execution observations for one declared collection on one process.</summary>
/// <param name="Collection">The declared collection alias.</param>
/// <param name="Provider">The declared provider alias.</param>
/// <param name="Scope">The explicit observation scope, always NodeLocal for execution counters.</param>
/// <param name="Availability">Whether numeric observations are supported.</param>
/// <param name="StartedAt">When this process began collecting the slot.</param>
/// <param name="ObservedAt">The snapshot observation timestamp.</param>
/// <param name="Counters">Cumulative bounded execution facts, or null values when unavailable.</param>
/// <param name="Active">Current process-local work, or null values when unavailable.</param>
/// <param name="Latencies">Actual bounded sample quantiles with independent availability and window.</param>
/// <param name="HotFootprint">Lifecycle-based retained representation capacities, or null when observations are disabled/unavailable.</param>
/// <example><code>var hits = snapshot.Counters[CacheObservationKind.PositiveHit];</code></example>
public sealed record CacheObservationSnapshot(string Collection, string Provider, string Scope, CacheObservationAvailability Availability,
    DateTimeOffset StartedAt, DateTimeOffset ObservedAt, IReadOnlyDictionary<CacheObservationKind, long?> Counters,
    IReadOnlyDictionary<CacheActiveKind, long?> Active, IReadOnlyDictionary<CacheLatencyKind, CacheLatencySnapshot> Latencies, CacheHotFootprintSnapshot HotFootprint = null);

/// <summary>Reports quantiles calculated from retained actual duration samples, never from averages.</summary>
/// <param name="Availability">Whether retained samples support quantiles.</param>
/// <param name="Accuracy">Sampled for bounded duration samples, Unavailable otherwise.</param>
/// <param name="SamplesObserved">Actual observations since the slot began.</param>
/// <param name="RetainedSamples">Actual samples retained for the current bounded window.</param>
/// <param name="OldestSampleAt">The oldest retained sample timestamp.</param>
/// <param name="NewestSampleAt">The newest retained sample timestamp.</param>
/// <param name="P50Seconds">The retained sample median, or null when unavailable.</param>
/// <param name="P95Seconds">The retained sample ninety-fifth percentile, or null when unavailable.</param>
/// <param name="P99Seconds">The retained sample ninety-ninth percentile, or null when unavailable.</param>
/// <example><code>if (latency.P95Seconds is { } seconds) { ShowSampled(seconds); }</code></example>
public sealed record CacheLatencySnapshot(CacheObservationAvailability Availability, CacheObservationAccuracy Accuracy, long SamplesObserved,
    int RetainedSamples, DateTimeOffset? OldestSampleAt, DateTimeOffset? NewestSampleAt, double? P50Seconds, double? P95Seconds, double? P99Seconds);

/// <summary>Attaches scope, accuracy, freshness and absence semantics to one diagnostic measurement.</summary>
/// <typeparam name="T">The measured value type.</typeparam>
/// <param name="Value">The value, which must only be consumed when availability is Available.</param>
/// <param name="Availability">Whether the underlying source supports this observation.</param>
/// <param name="Accuracy">How the measurement was established.</param>
/// <param name="Scope">NodeLocal, HostLocalSharedStore, or SharedStore as supported by the source.</param>
/// <param name="ObservedAt">When the value was observed, or null when unavailable.</param>
/// <param name="Reason">A bounded absence category without native secrets.</param>
/// <example><code>if (value.Availability == CacheObservationAvailability.Available) { Show(value.Value); }</code></example>
public sealed record CacheObservedValue<T>(T Value, CacheObservationAvailability Availability, CacheObservationAccuracy Accuracy,
    string Scope, DateTimeOffset? ObservedAt, string Reason = null);