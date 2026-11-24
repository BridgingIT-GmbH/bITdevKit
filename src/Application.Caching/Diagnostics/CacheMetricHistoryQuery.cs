// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Selects a bounded process-local diagnostic history without provider or payload access.</summary>
/// <param name="Collection">The declared collection alias, or null for node aggregates.</param>
/// <param name="Series">Up to sixteen predeclared series identifiers; null selects the standard series for this scope.</param>
/// <param name="Window">A positive displayed window within retained history, or the configured default.</param>
/// <param name="MaximumPoints">Maximum returned points per series, never more than the configured ceiling or 180.</param>
/// <example><code>var history = await metrics.QueryAsync(new("cards", Window: TimeSpan.FromMinutes(15)), token);</code></example>
public sealed record CacheMetricHistoryQuery(string Collection = null, IReadOnlyList<string> Series = null, TimeSpan? Window = null, int MaximumPoints = 180);

/// <summary>Identifies one configured diagnostic series with explicit unit, scope and aggregation semantics.</summary>
/// <param name="Id">The bounded declared series identifier.</param>
/// <param name="Collection">The declared collection alias, or null for node aggregates.</param>
/// <param name="Metric">The controlled event, gauge or sampled-quantile family.</param>
/// <param name="Unit">Events/s, entries/s, work, bytes, percent, or seconds.</param>
/// <param name="Aggregation">DurationWeightedRate for counters, Last for gauges and sampled quantiles.</param>
/// <param name="Scope">NodeLocal; provider storage counts are never summed into these execution series.</param>
/// <param name="Accuracy">Exact observations, sampled histograms/process measurements, or an unavailable source.</param>
/// <example><code>ShowLegend(series.Metric, series.Unit, series.Scope);</code></example>
public sealed record CacheMetricSeriesDescriptor(string Id, string Collection, string Metric, string Unit, string Aggregation, string Scope, CacheObservationAccuracy Accuracy = CacheObservationAccuracy.Exact);

/// <summary>Preserves real zero measurements and missing observations as different values.</summary>
/// <param name="At">The observation or downsampled interval-end timestamp.</param>
/// <param name="Value">The measured value, or null when unavailable.</param>
/// <param name="Availability">Whether this interval supports a numeric observation.</param>
/// <param name="Seconds">The actual observation interval used for counter rates.</param>
/// <example><code>if (point.Value is { } value) { Plot(point.At, value); }</code></example>
public readonly record struct CacheMetricPoint(DateTimeOffset At, double? Value, CacheObservationAvailability Availability, double Seconds = 0);

/// <summary>Returns one bounded history series with actual observations and declared downsampling semantics.</summary>
/// <param name="Descriptor">The predeclared bounded series metadata.</param>
/// <param name="Points">At most the requested finite point count.</param>
/// <param name="Availability">The series source state.</param>
/// <param name="Reason">A bounded unavailability reason, or null when available.</param>
/// <example><code>foreach (var point in series.Points) { Plot(point); }</code></example>
public sealed record CacheMetricSeriesResult(CacheMetricSeriesDescriptor Descriptor, IReadOnlyList<CacheMetricPoint> Points,
    CacheObservationAvailability Availability, string Reason = null);

/// <summary>Reports a bounded real history window without synthesizing pre-startup observations or unavailable quantiles.</summary>
/// <param name="Availability">Whether the optional history sampler is active.</param>
/// <param name="ObservedAt">The query observation timestamp.</param>
/// <param name="AvailableFrom">The earliest actual retained sample, or null.</param>
/// <param name="AvailableTo">The newest actual retained sample, or null.</param>
/// <param name="Scope">NodeLocal for the entire report.</param>
/// <param name="Series">The selected bounded histories.</param>
/// <example><code>ShowAvailableWindow(history.AvailableFrom, history.AvailableTo);</code></example>
public sealed record CacheMetricHistoryResult(CacheObservationAvailability Availability, DateTimeOffset ObservedAt, DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo, string Scope, IReadOnlyList<CacheMetricSeriesResult> Series);