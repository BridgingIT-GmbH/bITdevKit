// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records explicit observation and bounded history policies without starting sampling.</summary>
/// <example><code>diagnostics.HistoryRetention(TimeSpan.FromHours(1)).ChartWindow(TimeSpan.FromMinutes(30));</code></example>
public sealed class CacheDiagnosticsBuilder : PatchOptionsBuilder<CacheDiagnosticsOptions, CacheDiagnosticsBuilder>
{
    /// <summary>Enables or disables intrinsic diagnostics.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.Enabled(false);</code></example>
    public CacheDiagnosticsBuilder Enabled(bool value) => this.With(options => options.Enabled, value);

    /// <summary>Sets the history sampling interval.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.SamplingInterval(TimeSpan.FromSeconds(10));</code></example>
    public CacheDiagnosticsBuilder SamplingInterval(TimeSpan value) => this.With(options => options.SamplingInterval, value);

    /// <summary>Sets the initial chart window without changing history retention.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.ChartWindow(TimeSpan.FromMinutes(5));</code></example>
    public CacheDiagnosticsBuilder ChartWindow(TimeSpan value) => this.With(options => options.ChartWindow, value);

    /// <summary>Sets the finite retained-series ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.MaximumHistorySeries(128);</code></example>
    public CacheDiagnosticsBuilder MaximumHistorySeries(int value) => this.With(options => options.MaximumHistorySeries, value);

    /// <summary>Sets the finite simultaneous chart-query ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.MaximumChartQueries(2);</code></example>
    public CacheDiagnosticsBuilder MaximumChartQueries(int value) => this.With(options => options.MaximumChartQueries, value);

    /// <summary>Sets the finite returned point ceiling for each chart series.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.MaximumChartPoints(120);</code></example>
    public CacheDiagnosticsBuilder MaximumChartPoints(int value) => this.With(options => options.MaximumChartPoints, value);

    /// <summary>Sets the finite retained duration-sample count.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.LatencySampleCapacity(128);</code></example>
    public CacheDiagnosticsBuilder LatencySampleCapacity(int value) => this.With(options => options.LatencySampleCapacity, value);

    /// <summary>Sets the maximum retained duration-sample age.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.LatencySampleWindow(TimeSpan.FromMinutes(10));</code></example>
    public CacheDiagnosticsBuilder LatencySampleWindow(TimeSpan value) => this.With(options => options.LatencySampleWindow, value);

    /// <summary>Sets the finite tracked-operation record ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.MaximumOperationRecords(500);</code></example>
    public CacheDiagnosticsBuilder MaximumOperationRecords(int value) => this.With(options => options.MaximumOperationRecords, value);

    /// <summary>Sets terminal operation retention.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.OperationRetention(TimeSpan.FromHours(12));</code></example>
    public CacheDiagnosticsBuilder OperationRetention(TimeSpan value) => this.With(options => options.OperationRetention, value);

    /// <summary>Explicitly enables or disables bounded metric history.</summary>
    /// <param name="enabled">Whether history sampling is enabled.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.History();</code></example>
    public CacheDiagnosticsBuilder History(bool enabled = true) => this.With(options => options.HistoryEnabled, enabled);

    /// <summary>Enables metric history and sets its retention without clamping other configured durations.</summary>
    /// <param name="retention">The finite retention, validated with the final sampling interval and chart window.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.HistoryRetention(TimeSpan.FromHours(1));</code></example>
    public CacheDiagnosticsBuilder HistoryRetention(TimeSpan retention) => this.With(options => options.HistoryEnabled, true).With(options => options.HistoryRetention, retention);

    /// <summary>Explicitly enables or disables bounded per-key history.</summary>
    /// <param name="enabled">Whether per-key history is retained.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>diagnostics.DetailedKeyHistory();</code></example>
    public CacheDiagnosticsBuilder DetailedKeyHistory(bool enabled = true) => this.With(options => options.DetailedKeyHistory, enabled);
}