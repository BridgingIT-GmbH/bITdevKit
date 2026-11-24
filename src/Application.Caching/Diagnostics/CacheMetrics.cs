// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>Exports bounded intrinsic facts through the DevKit metrics service and standard cache instruments.</summary>
/// <param name="metrics">The optional existing DevKit exporter.</param>
/// <example><code>var metrics = new CacheMetrics(existingMetricsService);</code></example>
public sealed class CacheMetrics(IMetricsService metrics = null)
{
    /// <summary>The stable cache meter and ActivitySource identity.</summary>
    public const string InstrumentationName = "BridgingIT.DevKit.Application.Caching";

    private static readonly Meter Meter = new(InstrumentationName);
    private static readonly ActivitySource Activities = new(InstrumentationName);
    private static readonly string[] CounterNames = Enum.GetNames<CacheObservationKind>().Select(name => "cache.fact." + name).ToArray();
    private static readonly string[] DurationNames = Enum.GetNames<CacheLatencyKind>().Select(name => "cache.duration." + name).ToArray();
    private static readonly Counter<long>[] Counters = CounterNames.Select(name => Meter.CreateCounter<long>(name, "{event}")).ToArray();
    private static readonly Histogram<double>[] Durations = DurationNames.Select(name => Meter.CreateHistogram<double>(name, "s")).ToArray();

    /// <summary>Exports one counter fact using startup-created collection/provider/strategy labels.</summary>
    /// <param name="kind">The fixed execution fact.</param>
    /// <param name="value">The nonnegative event or entry count.</param>
    /// <param name="tags">Only startup-declared bounded aliases and controlled strategy values.</param>
    public void Record(CacheObservationKind kind, long value, ReadOnlySpan<MetricTag> tags)
    {
        try
        {
            metrics?.AddCounter(CounterNames[(int)kind], value, tags);
            if (!Counters[(int)kind].Enabled) { return; }

            var labels = Labels(tags);
            Counters[(int)kind].Add(value, labels);
        }
        catch { /* Observation/export failure cannot change cache correctness or an acknowledged result. */ }
    }

    /// <summary>Exports an actual duration sample in seconds.</summary>
    /// <param name="kind">The fixed latency family.</param>
    /// <param name="seconds">The observed finite nonnegative duration.</param>
    /// <param name="tags">Only startup-declared bounded aliases and controlled strategy values.</param>
    public void RecordDuration(CacheLatencyKind kind, double seconds, ReadOnlySpan<MetricTag> tags)
    {
        try
        {
            metrics?.RecordHistogram(DurationNames[(int)kind], seconds, "s", tags);
            if (!Durations[(int)kind].Enabled) { return; }

            var labels = Labels(tags);
            Durations[(int)kind].Record(seconds, labels);
        }
        catch { /* Metrics callbacks and exporters are outside the correctness boundary. */ }
    }

    /// <summary>Starts an optional correlated trace using bounded names and aliases.</summary>
    /// <param name="operation">The controlled operation category.</param>
    /// <param name="collection">The declared collection alias, or null for provider-wide work.</param>
    /// <param name="provider">The declared provider alias.</param>
    /// <returns>A normal activity, or null when no listener observes it.</returns>
    /// <example><code>using var activity = metrics.StartActivity(CacheOperationKind.Get, "CARDS", "DATABASE");</code></example>
    public Activity StartActivity(CacheOperationKind operation, string collection, string provider)
    {
        try
        {
            if (!Enum.IsDefined(operation) || !Activities.HasListeners()) { return null; }

            return Activities.StartActivity("cache." + operation)?.SetTag("cache.collection", collection)?.SetTag("cache.provider", provider);
        }
        catch { return null; }
    }

    private static TagList Labels(ReadOnlySpan<MetricTag> tags)
    {
        var result = new TagList();
        foreach (var tag in tags) { result.Add(tag.Name, tag.Value); }

        return result;
    }
}