// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Bounds process-local operational observations.</summary>
/// <example><code>var options = new CacheDiagnosticsOptions();</code></example>
public sealed record CacheDiagnosticsOptions
{
    /// <summary>Gets whether inexpensive bounded process-local execution counters and samples are collected.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets whether the optional bounded metric-history sampler runs.</summary>
    public bool HistoryEnabled { get; init; } = false;

    /// <summary>Gets the history sampling interval, bounded between one second and five minutes.</summary>
    public TimeSpan SamplingInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets retained history, bounded to one hour.</summary>
    public TimeSpan HistoryRetention { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets the maximum configured process-local history series, at most 256.</summary>
    public int MaximumHistorySeries { get; init; } = 256;

    /// <summary>Gets simultaneous bounded chart queries, at most two.</summary>
    public int MaximumChartQueries { get; init; } = 2;

    /// <summary>Gets retained actual duration samples per collection and latency family, at most 4,096.</summary>
    public int LatencySampleCapacity { get; init; } = 256;

    /// <summary>Gets the maximum age of retained latency samples, bounded to one hour.</summary>
    public TimeSpan LatencySampleWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets whether explicitly enabled bounded per-key history is recorded.</summary>
    public bool DetailedKeyHistory { get; init; } = false;

    /// <summary>Gets the normal metrics chart window, which cannot exceed retained history.</summary>
    public TimeSpan ChartWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets maximum returned points per series.</summary>
    public int MaximumChartPoints { get; init; } = 180;

    /// <summary>Gets tracked provider operation record ceiling.</summary>
    public int MaximumOperationRecords { get; init; } = 1000;

    /// <summary>Gets terminal operation retention; active records are not silently evicted.</summary>
    public TimeSpan OperationRetention { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Validates finite diagnostic allocation, history and query limits before optional observation state is created.</summary>
    /// <exception cref="ArgumentException">A requested diagnostic limit exceeds its supported finite bounds.</exception>
    /// <example><code>new CacheDiagnosticsOptions { HistoryEnabled = true }.Validate();</code></example>
    public void Validate()
    {
        if (this.SamplingInterval < TimeSpan.FromSeconds(1) || this.SamplingInterval > TimeSpan.FromMinutes(5) ||
            this.HistoryRetention < this.SamplingInterval || this.HistoryRetention > TimeSpan.FromHours(1) ||
            this.MaximumHistorySeries is <= 0 or > 256 || this.MaximumChartQueries is <= 0 or > 2 ||
            this.LatencySampleCapacity is <= 0 or > 4096 || this.LatencySampleWindow <= TimeSpan.Zero || this.LatencySampleWindow > TimeSpan.FromHours(1) ||
            this.MaximumChartPoints is <= 0 or > 180 || this.ChartWindow <= TimeSpan.Zero || this.ChartWindow > TimeSpan.FromHours(1) ||
            this.MaximumOperationRecords is <= 0 or > 1000 || this.OperationRetention <= TimeSpan.Zero || this.OperationRetention > TimeSpan.FromHours(24))
        {
            throw new ArgumentException("Diagnostic history, samples, queries and operation retention must remain within their finite supported limits.");
        }

        if (this.ChartWindow > this.HistoryRetention)
        {
            throw new ArgumentException("Diagnostics.ChartWindow cannot exceed Diagnostics.HistoryRetention.");
        }
    }
}