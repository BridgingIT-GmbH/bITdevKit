// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides an easy-to-use abstraction for applications that want to emit custom devkit metrics.
/// </summary>
/// <example>
/// <code>
/// public sealed class InventoryService(IMetricsService metrics)
/// {
///     public async Task RefreshAsync()
///     {
///         using var operation = metrics.Track("inventory_refresh", "warehouse_a");
///         await Task.Delay(10);
///     }
/// }
/// </code>
/// </example>
public interface IMetricsService
{
    /// <summary>
    /// Builds a metric series name from a family and optional dynamic parts.
    /// </summary>
    /// <param name="family">The metric family.</param>
    /// <param name="parts">Optional dynamic suffix parts.</param>
    /// <returns>The normalized metric series name.</returns>
    string Series(string family, params string[] parts);

    /// <summary>
    /// Captures a start timestamp for later duration recording.
    /// </summary>
    /// <returns>The current stopwatch timestamp.</returns>
    long StartTimestamp();

    /// <summary>
    /// Increments a cumulative counter series by one.
    /// </summary>
    /// <param name="family">The metric family.</param>
    /// <param name="parts">Optional dynamic suffix parts.</param>
    void Increment(string family, params string[] parts);

    /// <summary>
    /// Increments a failure counter series by one.
    /// </summary>
    /// <param name="family">The base metric family.</param>
    /// <param name="parts">Optional dynamic suffix parts.</param>
    void IncrementFailure(string family, params string[] parts);

    /// <summary>
    /// Adjusts a current live-view series.
    /// </summary>
    /// <param name="family">The base metric family.</param>
    /// <param name="value">The delta to apply.</param>
    /// <param name="parts">Optional dynamic suffix parts.</param>
    void ChangeCurrent(string family, int value, params string[] parts);

    /// <summary>
    /// Records a duration histogram value in milliseconds.
    /// </summary>
    /// <param name="family">The base metric family.</param>
    /// <param name="startedTimestamp">The timestamp captured earlier.</param>
    /// <param name="parts">Optional dynamic suffix parts.</param>
    void RecordDuration(string family, long startedTimestamp, params string[] parts);

    /// <summary>
    /// Adds a value to a cumulative counter.
    /// </summary>
    /// <param name="name">The complete instrument name.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="tags">Optional measurement tags.</param>
    void AddCounter(
        string name,
        long value = 1,
        ReadOnlySpan<MetricTag> tags = default);

    /// <summary>
    /// Adds a delta to an up/down counter.
    /// </summary>
    /// <param name="name">The complete instrument name.</param>
    /// <param name="value">The positive or negative delta.</param>
    /// <param name="tags">Optional measurement tags.</param>
    void AddUpDownCounter(
        string name,
        long value,
        ReadOnlySpan<MetricTag> tags = default);

    /// <summary>
    /// Records a long histogram sample.
    /// </summary>
    /// <param name="name">The complete instrument name.</param>
    /// <param name="value">The sample value.</param>
    /// <param name="unit">The optional measurement unit.</param>
    /// <param name="tags">Optional measurement tags.</param>
    void RecordHistogram(
        string name,
        long value,
        string unit = null,
        ReadOnlySpan<MetricTag> tags = default);

    /// <summary>
    /// Records a double histogram sample.
    /// </summary>
    /// <param name="name">The complete instrument name.</param>
    /// <param name="value">The sample value.</param>
    /// <param name="unit">The optional measurement unit.</param>
    /// <param name="tags">Optional measurement tags.</param>
    void RecordHistogram(
        string name,
        double value,
        string unit = null,
        ReadOnlySpan<MetricTag> tags = default);

    /// <summary>
    /// Records elapsed milliseconds since a captured timestamp.
    /// </summary>
    /// <param name="name">The complete histogram name.</param>
    /// <param name="startedTimestamp">The timestamp returned by <see cref="StartTimestamp"/>.</param>
    /// <param name="tags">Optional measurement tags.</param>
    void RecordHistogramDuration(
        string name,
        long startedTimestamp,
        ReadOnlySpan<MetricTag> tags = default);

    /// <summary>
    /// Sets the current value exposed by an observable gauge.
    /// </summary>
    /// <param name="name">The complete instrument name.</param>
    /// <param name="value">The current gauge value.</param>
    void SetGauge(string name, long value);

    /// <summary>
    /// Starts a tracked metrics scope that increments totals and current values and records duration on dispose.
    /// </summary>
    /// <param name="family">The base metric family.</param>
    /// <param name="parts">Optional dynamic suffix parts.</param>
    /// <returns>A disposable scope that completes the tracked operation.</returns>
    IDisposable Track(string family, params string[] parts);
}

/// <summary>
/// Default implementation of <see cref="IMetricsService"/> backed by the shared devkit meter.
/// </summary>
public sealed class MetricsService : IMetricsService, IDisposable
{
    private readonly ConcurrentDictionary<string, InstrumentDefinition> definitions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Counter<long>>> counters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<UpDownCounter<long>>> upDownCounters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Name, string Unit), Lazy<Histogram<long>>> longHistograms = new();
    private readonly ConcurrentDictionary<(string Name, string Unit), Lazy<Histogram<double>>> doubleHistograms = new();
    private readonly ConcurrentDictionary<string, long> gaugeValues = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<ObservableGauge<long>>> gauges = new(StringComparer.Ordinal);
    private readonly Meter meter;
    private readonly bool ownsMeter;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsService"/> class.
    /// </summary>
    /// <param name="meterFactory">The optional factory used exclusively by this service to create the shared devkit meter.</param>
    /// <example>
    /// <code>
    /// services.AddMetrics(options => options.Enabled());
    /// </code>
    /// </example>
    public MetricsService(IMeterFactory meterFactory = null)
    {
        if (meterFactory is null)
        {
            this.meter = new Meter(Metrics.MeterName);
            this.ownsMeter = true;
            return;
        }

        try
        {
            this.meter = meterFactory.Create(Metrics.MeterName);
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            this.meter = null;
        }
    }

    /// <inheritdoc />
    public string Series(string family, params string[] parts)
    {
        return Metrics.Series(family, parts);
    }

    /// <inheritdoc />
    public long StartTimestamp()
    {
        return Metrics.StartTimestamp();
    }

    /// <inheritdoc />
    public void Increment(string family, params string[] parts)
    {
        this.AddCounter(this.Series(family, parts));
    }

    /// <inheritdoc />
    public void IncrementFailure(string family, params string[] parts)
    {
        var series = this.Series(family, parts);
        this.AddCounter(Metrics.FailureSeries(series));
    }

    /// <inheritdoc />
    public void ChangeCurrent(string family, int value, params string[] parts)
    {
        var series = this.Series(family, parts);
        this.AddUpDownCounter(Metrics.CurrentSeries(series), value);
    }

    /// <inheritdoc />
    public void RecordDuration(string family, long startedTimestamp, params string[] parts)
    {
        var series = this.Series(family, parts);
        this.RecordHistogramDuration(Metrics.DurationSeries(series), startedTimestamp);
    }

    /// <inheritdoc />
    public void AddCounter(
        string name,
        long value = 1,
        ReadOnlySpan<MetricTag> tags = default)
    {
        if (this.IsUnavailable ||
            string.IsNullOrWhiteSpace(name) ||
            !this.TryRegisterDefinition(name, InstrumentKind.Counter, null))
        {
            return;
        }

        try
        {
            var counter = this.counters.GetOrAdd(
                name,
                key => new Lazy<Counter<long>>(
                    () => this.meter.CreateCounter<long>(key),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            var tagList = CreateTagList(tags);
            counter.Add(value, in tagList);
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
        }
    }

    /// <inheritdoc />
    public void AddUpDownCounter(
        string name,
        long value,
        ReadOnlySpan<MetricTag> tags = default)
    {
        if (this.IsUnavailable ||
            string.IsNullOrWhiteSpace(name) ||
            value == 0 ||
            !this.TryRegisterDefinition(name, InstrumentKind.UpDownCounter, null))
        {
            return;
        }

        try
        {
            var counter = this.upDownCounters.GetOrAdd(
                name,
                key => new Lazy<UpDownCounter<long>>(
                    () => this.meter.CreateUpDownCounter<long>(key),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            var tagList = CreateTagList(tags);
            counter.Add(value, in tagList);
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
        }
    }

    /// <inheritdoc />
    public void RecordHistogram(
        string name,
        long value,
        string unit = null,
        ReadOnlySpan<MetricTag> tags = default)
    {
        if (this.IsUnavailable ||
            string.IsNullOrWhiteSpace(name) ||
            !this.TryRegisterDefinition(name, InstrumentKind.LongHistogram, unit))
        {
            return;
        }

        try
        {
            var histogram = this.longHistograms.GetOrAdd(
                (name, unit),
                key => new Lazy<Histogram<long>>(
                    () => this.meter.CreateHistogram<long>(key.Name, key.Unit),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            var tagList = CreateTagList(tags);
            histogram.Record(value, in tagList);
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
        }
    }

    /// <inheritdoc />
    public void RecordHistogram(
        string name,
        double value,
        string unit = null,
        ReadOnlySpan<MetricTag> tags = default)
    {
        if (this.IsUnavailable ||
            string.IsNullOrWhiteSpace(name) ||
            !this.TryRegisterDefinition(name, InstrumentKind.DoubleHistogram, unit))
        {
            return;
        }

        try
        {
            var histogram = this.doubleHistograms.GetOrAdd(
                (name, unit),
                key => new Lazy<Histogram<double>>(
                    () => this.meter.CreateHistogram<double>(key.Name, key.Unit),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            var tagList = CreateTagList(tags);
            histogram.Record(value, in tagList);
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
        }
    }

    /// <inheritdoc />
    public void RecordHistogramDuration(
        string name,
        long startedTimestamp,
        ReadOnlySpan<MetricTag> tags = default)
    {
        this.RecordHistogram(
            name,
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            "ms",
            tags);
    }

    /// <inheritdoc />
    public void SetGauge(string name, long value)
    {
        if (this.IsUnavailable ||
            string.IsNullOrWhiteSpace(name) ||
            !this.TryRegisterDefinition(name, InstrumentKind.ObservableGauge, null))
        {
            return;
        }

        try
        {
            this.gaugeValues[name] = value;
            _ = this.gauges.GetOrAdd(
                name,
                key => new Lazy<ObservableGauge<long>>(
                    () => this.meter.CreateObservableGauge(
                        key,
                        () => this.gaugeValues.GetValueOrDefault(key)),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
        }
    }

    /// <inheritdoc />
    public IDisposable Track(string family, params string[] parts)
    {
        var series = this.Series(family, parts);
        var startedTimestamp = this.StartTimestamp();

        this.AddCounter(series);
        this.AddUpDownCounter(Metrics.CurrentSeries(series), 1);

        return new Scope(this, series, startedTimestamp);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0 && this.ownsMeter)
        {
            this.meter?.Dispose();
        }
    }

    private bool IsUnavailable => this.meter is null || Volatile.Read(ref this.disposed) != 0;

    private bool TryRegisterDefinition(string name, InstrumentKind kind, string unit)
    {
        var requested = new InstrumentDefinition(kind, unit);
        var registered = this.definitions.GetOrAdd(name, requested);

        return registered == requested;
    }

    private static TagList CreateTagList(ReadOnlySpan<MetricTag> tags)
    {
        var result = new TagList();
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag.Name))
            {
                result.Add(tag.Name, tag.Value);
            }
        }

        return result;
    }

    private static bool IsFatal(Exception exception)
    {
        return exception is OutOfMemoryException or StackOverflowException or AccessViolationException;
    }

    private sealed class Scope(MetricsService metricsService, string series, long startedTimestamp) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            metricsService.AddUpDownCounter(Metrics.CurrentSeries(series), -1);
            metricsService.RecordHistogramDuration(Metrics.DurationSeries(series), startedTimestamp);
        }
    }

    private readonly record struct InstrumentDefinition(InstrumentKind Kind, string Unit);

    private enum InstrumentKind
    {
        Counter,
        UpDownCounter,
        LongHistogram,
        DoubleHistogram,
        ObservableGauge,
    }
}
