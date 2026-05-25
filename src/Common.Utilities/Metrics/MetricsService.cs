// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics.Metrics;

/// <summary>
/// Provides an easy-to-use abstraction for applications that want to emit custom devkit metrics.
/// </summary>
/// <example>
/// <code>
/// public sealed class InventoryService(IDevKitMetricsService metrics)
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
public sealed class MetricsService(IMeterFactory meterFactory) : IMetricsService
{
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
        Metrics.Increment(meterFactory, this.Series(family, parts));
    }

    /// <inheritdoc />
    public void IncrementFailure(string family, params string[] parts)
    {
        var series = this.Series(family, parts);
        Metrics.Increment(meterFactory, Metrics.FailureSeries(series));
    }

    /// <inheritdoc />
    public void ChangeCurrent(string family, int value, params string[] parts)
    {
        var series = this.Series(family, parts);
        Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(series), value);
    }

    /// <inheritdoc />
    public void RecordDuration(string family, long startedTimestamp, params string[] parts)
    {
        var series = this.Series(family, parts);
        Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(series), startedTimestamp);
    }

    /// <inheritdoc />
    public IDisposable Track(string family, params string[] parts)
    {
        var series = this.Series(family, parts);
        var startedTimestamp = this.StartTimestamp();

        Metrics.Increment(meterFactory, series);
        Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(series), 1);

        return new Scope(meterFactory, series, startedTimestamp);
    }

    private sealed class Scope(IMeterFactory meterFactory, string series, long startedTimestamp) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            Metrics.ChangeCurrent(meterFactory, Metrics.CurrentSeries(series), -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(series), startedTimestamp);
        }
    }
}