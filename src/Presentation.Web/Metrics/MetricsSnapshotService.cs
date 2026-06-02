// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides live snapshots of the shared devkit meter grouped by feature area.
/// </summary>
/// <example>
/// <code>
/// var snapshot = snapshotService.GetSnapshot();
/// var available = snapshot.AvailableMetrics;
/// </code>
/// </example>
public interface IMetricsSnapshotService
{
    /// <summary>
    /// Captures the current devkit metrics snapshot.
    /// </summary>
    /// <returns>The grouped metrics snapshot.</returns>
    MetricsSnapshotModel GetSnapshot();
}

/// <summary>
/// Listens to the shared devkit meter and exposes grouped snapshots for system metrics endpoints.
/// </summary>
/// <example>
/// <code>
/// var snapshot = snapshotService.GetSnapshot();
/// var messaging = snapshot.Features["messaging"];
/// </code>
/// </example>
public class MetricsSnapshotService : IMetricsSnapshotService, IDisposable
{
    private static readonly HashSet<string> BaseSuccessSeries =
    [
        "messaging_publish",
        "messaging_handle",
        "domainevents_create",
        "requester_send",
        "requester_handle",
        "notifier_publish",
        "notifier_handle",
        "queueing_enqueue",
        "queueing_handle",
        "repositories_read",
        "repositories_write",
        "repositories_delete",
        "activeentity_read",
        "activeentity_write",
        "activeentity_delete",
        "jobscheduling_execute",
        "jobs_executions_completed",
        "jobs_occurrences_materialized",
        "jobs_events_accepted",
        "orchestrations_activity_execute",
        "orchestrations_finish"
    ];

    private static readonly HashSet<string> BaseFailureSeries =
    [
        "messaging_publish_failure",
        "messaging_handle_failure",
        "requester_send_failure",
        "requester_handle_failure",
        "notifier_publish_failure",
        "notifier_handle_failure",
        "queueing_enqueue_failure",
        "queueing_handle_failure",
        "repositories_read_failure",
        "repositories_write_failure",
        "repositories_delete_failure",
        "activeentity_read_failure",
        "activeentity_write_failure",
        "activeentity_delete_failure",
        "jobscheduling_execute_failure",
        "jobs_executions_failed",
        "jobs_executions_retried",
        "jobs_executions_timedout",
        "jobs_executions_cancelled",
        "jobs_executions_interrupted",
        "orchestrations_activity_execute_failure",
        "orchestrations_finish_failure"
    ];

    private static readonly HashSet<string> BaseCurrentSeries =
    [
        "messaging_publish_current",
        "messaging_handle_current",
        "requester_send_current",
        "requester_handle_current",
        "notifier_publish_current",
        "notifier_handle_current",
        "queueing_enqueue_current",
        "queueing_handle_current",
        "repositories_read_current",
        "repositories_write_current",
        "repositories_delete_current",
        "jobscheduling_execute_current",
        "jobs_executions_active",
        "orchestrations_activity_execute_current"
    ];

    private readonly MeterListener listener = new();
    private readonly ConcurrentDictionary<string, double> counters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HistogramAccumulator> histograms = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, MetricInstrumentKind> instruments = new(StringComparer.Ordinal);
    private readonly DateTimeOffset processStartedAtUtc = new(Process.GetCurrentProcess().StartTime.ToUniversalTime());

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsSnapshotService"/> class.
    /// </summary>
    public MetricsSnapshotService()
    {
        this.listener.InstrumentPublished = (instrument, listener) =>
        {
            if (!string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal))
            {
                return;
            }

            this.instruments[instrument.Name] = GetInstrumentKind(instrument);
            listener.EnableMeasurementEvents(instrument);
        };

        this.listener.SetMeasurementEventCallback<byte>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, measurement));
        this.listener.SetMeasurementEventCallback<short>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, measurement));
        this.listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, measurement));
        this.listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, measurement));
        this.listener.SetMeasurementEventCallback<float>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, measurement));
        this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, measurement));
        this.listener.SetMeasurementEventCallback<decimal>((instrument, measurement, tags, state) => this.RecordMeasurement(instrument, (double)measurement));
        this.listener.Start();
    }

    /// <inheritdoc />
    public MetricsSnapshotModel GetSnapshot()
    {
        var capturedAtUtc = DateTimeOffset.UtcNow;
        var snapshot = new MetricsSnapshotModel
        {
            CapturedAtUtc = capturedAtUtc,
            Meter = Metrics.MeterName,
            ProcessStartedAtUtc = this.processStartedAtUtc,
            UptimeSeconds = Math.Max(0, (capturedAtUtc - this.processStartedAtUtc).TotalSeconds)
        };

        foreach (var pair in this.counters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var featureName = GetFeatureName(pair.Key);
            if (featureName is null)
            {
                continue;
            }

            var feature = GetOrAddFeature(snapshot.Features, featureName);
            if (IsCurrentSeries(pair.Key) || this.instruments.GetValueOrDefault(pair.Key) == MetricInstrumentKind.UpDownCounter)
            {
                feature.Current[pair.Key] = pair.Value;
            }
            else
            {
                feature.Counters[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in this.histograms.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var featureName = GetFeatureName(pair.Key);
            if (featureName is null)
            {
                continue;
            }

            var feature = GetOrAddFeature(snapshot.Features, featureName);
            var value = pair.Value.GetSnapshot();
            feature.Durations[pair.Key] = new BdkHistogramSnapshotModel
            {
                Count = value.Count,
                Sum = value.Sum,
                Average = value.Count == 0 ? 0 : value.Sum / value.Count,
                Max = value.Max
            };
        }

        foreach (var feature in snapshot.Features.Values)
        {
            feature.SuccessTotal = feature.Counters
                .Where(pair => BaseSuccessSeries.Contains(pair.Key))
                .Sum(pair => pair.Value);
            feature.FailureTotal = feature.Counters
                .Where(pair => BaseFailureSeries.Contains(pair.Key))
                .Sum(pair => pair.Value);
            feature.CurrentTotal = feature.Current
                .Where(pair => BaseCurrentSeries.Contains(pair.Key))
                .Sum(pair => pair.Value);
            feature.TopThroughput = feature.Counters
                .Where(pair => !IsFailureSeries(pair.Key))
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(5)
                .Select(pair => new MetricValueModel { Name = pair.Key, Value = pair.Value })
                .ToList();
            feature.TopCurrent = feature.Current
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(5)
                .Select(pair => new MetricValueModel { Name = pair.Key, Value = pair.Value })
                .ToList();
            feature.TopFailures = feature.Counters
                .Where(pair => IsFailureSeries(pair.Key))
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(5)
                .Select(pair => new MetricValueModel { Name = pair.Key, Value = pair.Value })
                .ToList();
            feature.LatencyHighlights = feature.Durations
                .OrderByDescending(pair => pair.Value.Average)
                .ThenByDescending(pair => pair.Value.Max)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Take(5)
                .Select(pair => new MetricLatencyModel
                {
                    Name = pair.Key,
                    Count = pair.Value.Count,
                    Sum = pair.Value.Sum,
                    Average = pair.Value.Average,
                    Max = pair.Value.Max
                })
                .ToList();
        }

        snapshot.TopFailures = snapshot.Features.Values
            .SelectMany(feature => feature.TopFailures)
            .OrderByDescending(model => model.Value)
            .ThenBy(model => model.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();
        snapshot.TopThroughput = snapshot.Features.Values
            .SelectMany(feature => feature.TopThroughput)
            .OrderByDescending(model => model.Value)
            .ThenBy(model => model.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();
        snapshot.TopCurrent = snapshot.Features.Values
            .SelectMany(feature => feature.TopCurrent)
            .OrderByDescending(model => model.Value)
            .ThenBy(model => model.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();
        snapshot.LatencyHighlights = snapshot.Features.Values
            .SelectMany(feature => feature.LatencyHighlights)
            .OrderByDescending(model => model.Average)
            .ThenByDescending(model => model.Max)
            .ThenBy(model => model.Name, StringComparer.Ordinal)
            .Take(10)
            .ToList();
        snapshot.AvailableMetrics = this.BuildAvailableMetrics(snapshot.Features);

        return snapshot;
    }

    /// <summary>
    /// Releases the underlying meter listener.
    /// </summary>
    public void Dispose()
    {
        this.listener.Dispose();
    }

    private static BdkFeatureSnapshotModel GetOrAddFeature(Dictionary<string, BdkFeatureSnapshotModel> features, string featureName)
    {
        if (!features.TryGetValue(featureName, out var feature))
        {
            feature = new BdkFeatureSnapshotModel { Name = featureName };
            features[featureName] = feature;
        }

        return feature;
    }

    private static string GetFeatureName(string series)
    {
        if (series.StartsWith("messages_", StringComparison.Ordinal) || series.StartsWith("messaging_", StringComparison.Ordinal))
        {
            return "messaging";
        }

        if (series.StartsWith("domainevents_", StringComparison.Ordinal))
        {
            return "domain";
        }

        if (series.StartsWith("requester_", StringComparison.Ordinal))
        {
            return "requester";
        }

        if (series.StartsWith("notifier_", StringComparison.Ordinal))
        {
            return "notifier";
        }

        if (series.StartsWith("queueing_", StringComparison.Ordinal))
        {
            return "queueing";
        }

        if (series.StartsWith("repositories_", StringComparison.Ordinal))
        {
            return "repositories";
        }

        if (series.StartsWith("activeentity_", StringComparison.Ordinal))
        {
            return "activeentity";
        }

        if (series.StartsWith("jobscheduling_", StringComparison.Ordinal))
        {
            return "jobscheduling";
        }

        if (series.StartsWith("jobs_", StringComparison.Ordinal))
        {
            return "jobs";
        }

        if (series.StartsWith("orchestrations_", StringComparison.Ordinal))
        {
            return "orchestrations";
        }

        return null;
    }

    private static MetricInstrumentKind GetInstrumentKind(Instrument instrument)
    {
        if (!instrument.GetType().IsGenericType)
        {
            return MetricInstrumentKind.Counter;
        }

        var definition = instrument.GetType().GetGenericTypeDefinition();
        if (definition == typeof(Histogram<>))
        {
            return MetricInstrumentKind.Histogram;
        }

        return definition == typeof(UpDownCounter<>)
            ? MetricInstrumentKind.UpDownCounter
            : MetricInstrumentKind.Counter;
    }

    private static bool IsCurrentSeries(string series) =>
        series.EndsWith("_current", StringComparison.Ordinal) ||
        series.EndsWith(".active", StringComparison.Ordinal);

    private static bool IsFailureSeries(string series) =>
        series.EndsWith("_failure", StringComparison.Ordinal) ||
        series.EndsWith(".failed", StringComparison.Ordinal) ||
        series.EndsWith(".retried", StringComparison.Ordinal) ||
        series.EndsWith(".timedout", StringComparison.Ordinal) ||
        series.EndsWith(".cancelled", StringComparison.Ordinal) ||
        series.EndsWith(".interrupted", StringComparison.Ordinal) ||
        series.EndsWith("_failed", StringComparison.Ordinal) ||
        series.EndsWith("_retried", StringComparison.Ordinal) ||
        series.EndsWith("_timedout", StringComparison.Ordinal) ||
        series.EndsWith("_cancelled", StringComparison.Ordinal) ||
        series.EndsWith("_interrupted", StringComparison.Ordinal);

    private List<BdkMetricDescriptorModel> BuildAvailableMetrics(Dictionary<string, BdkFeatureSnapshotModel> features)
    {
        return features
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .SelectMany(pair =>
                pair.Value.Counters.Keys.Select(name => new BdkMetricDescriptorModel { Name = name, Feature = pair.Key, Kind = IsFailureSeries(name) ? "failure" : "total" })
                    .Concat(pair.Value.Current.Keys.Select(name => new BdkMetricDescriptorModel { Name = name, Feature = pair.Key, Kind = "current" }))
                    .Concat(pair.Value.Durations.Keys.Select(name => new BdkMetricDescriptorModel { Name = name, Feature = pair.Key, Kind = "duration", Unit = "ms" })))
            .DistinctBy(model => model.Name)
            .OrderBy(model => model.Feature, StringComparer.Ordinal)
            .ThenBy(model => model.Kind, StringComparer.Ordinal)
            .ThenBy(model => model.Name, StringComparer.Ordinal)
            .ToList();
    }

    private void RecordMeasurement<T>(Instrument instrument, T measurement)
        where T : struct, IConvertible
    {
        if (!this.instruments.TryGetValue(instrument.Name, out var kind))
        {
            return;
        }

        var numericValue = Convert.ToDouble(measurement);
        if (kind == MetricInstrumentKind.Histogram)
        {
            this.histograms.GetOrAdd(instrument.Name, _ => new HistogramAccumulator()).Add(numericValue);
            return;
        }

        this.counters.AddOrUpdate(instrument.Name, numericValue, (_, current) => current + numericValue);
    }

    private enum MetricInstrumentKind
    {
        Counter,
        UpDownCounter,
        Histogram
    }

    private sealed class HistogramAccumulator
    {
        private readonly Lock syncLock = new();
        private long count;
        private double sum;
        private double max;

        public void Add(double value)
        {
            lock (this.syncLock)
            {
                this.count++;
                this.sum += value;
                if (this.count == 1 || value > this.max)
                {
                    this.max = value;
                }
            }
        }

        public (long Count, double Sum, double Max) GetSnapshot()
        {
            lock (this.syncLock)
            {
                return (this.count, this.sum, this.max);
            }
        }
    }
}
