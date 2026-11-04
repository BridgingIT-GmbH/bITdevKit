// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

/// <summary>Collects a bounded, process-local history of aggregate counters and duration histograms.</summary>
/// <example><code>using var history = new DashboardMetricsHistory(chart.Series);</code></example>
public sealed class DashboardMetricsHistory : IDisposable
{
    private const int RetainedSeconds = 900;
    private readonly object gate = new();
    private readonly MeterListener listener = new();
    private readonly DashboardWorkloadSeries[] series;
    private readonly Dictionary<string, int> indices;
    private readonly SortedDictionary<long, Bucket> buckets = [];
    private readonly TimeProvider clock;
    private readonly DateTimeOffset started;
    private bool observed;
    private bool disposed;

    /// <summary>Starts listening immediately, independently of dashboard requests.</summary>
    /// <param name="series">Exact aggregate instrument names to collect.</param>
    /// <param name="timeProvider">The optional clock used for bucket boundaries.</param>
    public DashboardMetricsHistory(IEnumerable<DashboardWorkloadSeries> series, TimeProvider timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        this.series = series.DistinctBy(item => item.InstrumentName).ToArray();
        this.indices = this.series.Select((item, index) => (item.InstrumentName, index))
            .ToDictionary(item => item.InstrumentName, item => item.index, StringComparer.Ordinal);
        this.clock = timeProvider ?? TimeProvider.System;
        this.started = this.clock.GetUtcNow();
        this.listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == Metrics.MeterName && this.indices.ContainsKey(instrument.Name))
            {
                owner.EnableMeasurementEvents(instrument);
            }
        };
        this.listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => this.Record(instrument.Name, value));
        this.listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => this.Record(instrument.Name, value));
        this.listener.Start();
    }

    /// <summary>Returns completed one-second intervals; a partial startup interval uses its actual duration.</summary>
    /// <param name="enabled">Whether the host has enabled the metrics service. Unavailable values remain null.</param>
    /// <returns>A JSON-ready bounded snapshot with missing duration observations represented by null.</returns>
    public object Snapshot(bool enabled = true)
    {
        lock (this.gate)
        {
            var current = this.clock.GetUtcNow().ToUnixTimeSeconds();
            this.Trim(current);
            var first = Math.Max(this.started.ToUnixTimeSeconds(), current - RetainedSeconds);
            var samples = Enumerable.Range(0, (int)Math.Max(0, current - first)).Select(offset =>
            {
                var second = first + offset;
                this.buckets.TryGetValue(second, out var bucket);
                var seconds = Math.Min(1, ((second + 1) * 1000 - this.started.ToUnixTimeMilliseconds()) / 1000d);
                var values = new Dictionary<string, double?>(StringComparer.Ordinal);
                for (var i = 0; i < this.series.Length; i++)
                {
                    var sum = bucket?.Sums[i] ?? 0;
                    var count = bucket?.Counts[i] ?? 0;
                    values[this.series[i].InstrumentName] = !enabled ? null : this.series[i].IsDuration
                        ? count > 0 ? sum / count : null
                        : sum / seconds;
                }

                return new { at = DateTimeOffset.FromUnixTimeSeconds(second + 1), values };
            }).ToArray();
            return new { enabled, observed = this.observed, startedAt = this.started, bucketSeconds = 1, samples };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this.gate)
        {
            this.disposed = true;
        }

        this.listener.Dispose();
    }

    private void Record(string name, double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            return;
        }

        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            var second = this.clock.GetUtcNow().ToUnixTimeSeconds();
            this.Trim(second);
            if (!this.buckets.TryGetValue(second, out var bucket))
            {
                this.buckets[second] = bucket = new Bucket(this.series.Length);
            }

            var index = this.indices[name];
            bucket.Sums[index] += value;
            bucket.Counts[index]++;
            this.observed = true;
        }
    }

    private void Trim(long current)
    {
        while (this.buckets.Count > 0 && this.buckets.First().Key < current - RetainedSeconds)
        {
            this.buckets.Remove(this.buckets.First().Key);
        }
    }

    private sealed class Bucket(int size)
    {
        public double[] Sums { get; } = new double[size];

        public int[] Counts { get; } = new int[size];
    }
}
