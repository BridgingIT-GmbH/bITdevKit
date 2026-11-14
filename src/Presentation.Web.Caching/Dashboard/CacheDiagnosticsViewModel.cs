// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Describes one fixed chart family using only supported predeclared metric histories.</summary>
/// <param name="Key">Stable family key.</param>
/// <param name="Title">Accessible chart title.</param>
/// <param name="Unit">Shared vertical axis unit.</param>
/// <param name="Metrics">Controlled eligible source metric names.</param>
/// <example><code>var families = CacheDiagnosticsCharts.Families;</code></example>
public sealed record CacheDiagnosticsChartFamily(string Key, string Title, string Unit, IReadOnlyList<string> Metrics)
{
    /// <summary>Gets plain-text tooltip help for the chart heading without adding presentation help to history responses.</summary>
    /// <example><code>var tooltip = family.Description;</code></example>
    [JsonIgnore]
    public string Description { get; init; }
}

/// <summary>Returns one bounded chart with source availability preserved.</summary>
/// <param name="Family">Declared title, unit, and eligible measurements.</param>
/// <param name="Series">Actual sampled histories, with missing values left null.</param>
/// <param name="Availability">Current source state.</param>
/// <param name="Reason">Controlled unavailability explanation.</param>
/// <example><code>foreach (var series in chart.Series) { Plot(series.Points); }</code></example>
public sealed record CacheDiagnosticsChart(CacheDiagnosticsChartFamily Family, IReadOnlyList<CacheMetricSeriesResult> Series,
    CacheObservationAvailability Availability, string Reason = null);

/// <summary>Describes one finite actual observation window for six Dashboard chart families.</summary>
/// <param name="ObservedAt">The query timestamp.</param>
/// <param name="AvailableFrom">The earliest actual selected sample, or null.</param>
/// <param name="AvailableTo">The latest actual selected sample, or null.</param>
/// <param name="Scope">Explicit source scope.</param>
/// <param name="WindowMinutes">The requested bounded display window.</param>
/// <param name="Charts">The six fixed chart families.</param>
/// <example><code>var charts = await requester.SendAsync(historyRequest);</code></example>
public sealed record CacheDiagnosticsCharts(DateTimeOffset ObservedAt, DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableTo,
    string Scope, double WindowMinutes, IReadOnlyList<CacheDiagnosticsChart> Charts)
{
    /// <summary>Gets the focused charts shown before expanding the detailed diagnostic families.</summary>
    /// <remarks>Each overview selects existing family metrics without collecting additional history.</remarks>
    /// <example><code>foreach (var family in CacheDiagnosticsCharts.OverviewFamilies) { RenderOverview(family); }</code></example>
    public static IReadOnlyList<CacheDiagnosticsChartFamily> OverviewFamilies { get; } =
    [
        new("traffic", "Requests and hit ratio", "events/s", ["Request", "LookupHitRatio"])
        {
            Description = "Cache request rate and lookup hit ratio on this node. Hit ratio counts positive and cached-absence hits divided by hits plus misses."
        },
        new("latency", "Operation latency (p95)", "s", ["OperationP95"])
        {
            Description = "The 95th percentile of recorded operation durations, in seconds. About 95% of sampled operations finish within this duration. Missing samples are unavailable, not zero."
        },
        new("capacity", "Memory usage", "bytes", ["ResidentBytes", "TransientBytes"])
        {
            Description = "Cache memory accounted across all collections on this app node. Resident includes retained data and metadata; transient is temporary reserved working memory. This is separate from total process RAM."
        },
        new("reliability", "Errors and timeouts", "events/s", ["ProviderFailure", "SourceFailure", "WaitTimeout", "SourceTimeout"])
        {
            Description = "Provider/source failure and wait/source timeout rates observed on this node. A timeout can finish the caller's wait before the underlying work releases its resources."
        }
    ];

    /// <summary>Gets the fixed chart families; clients cannot supply arbitrary metric identifiers.</summary>
    public static IReadOnlyList<CacheDiagnosticsChartFamily> Families { get; } =
    [
        new("traffic", "Traffic and efficiency", "events/s", ["Request", "PositiveHit", "NegativeHit", "Miss", "HotBinaryReuse", "HotDecodedReuse", "LookupHitRatio"])
        {
            Description = "Node-local request, lookup-hit and miss rates, plus binary/decoded hot reuse. Hit ratio uses positive and cached-absence hits over hits plus misses."
        },
        new("throughput", "Successful entry throughput", "entries/s", ["ReadSuccess", "WriteSuccess", "InvalidatedEntries"])
        {
            Description = "Successful read, write and invalidated-entry rates on this node. These count entries affected, so batch operations can represent more than one entry per call."
        },
        new("latency", "Sampled operation latency", "s", ["OperationP50", "OperationP95", "OperationP99"])
        {
            Description = "Recorded operation duration percentiles in seconds: p50 is the median; p95 and p99 describe slower tail latency. Values come from retained samples, not averages."
        },
        new("shared", "Shared loading", "events/s", ["SourceAttempt", "Join", "ActiveOwners", "ActiveWaiters"])
        {
            Description = "Source attempts and joined callers show shared-loading work on this node. Active owners and waiters are current counts; rates are per second. Shared storage does not make these fleet totals."
        },
        new("capacity", "Owned resident capacity", "bytes", ["ResidentBytes", "HotResidentBytes", "TransientBytes", "HotBinaryBytes", "HotDecodedBytes", "HotPinnedBytes"])
        {
            Description = "Node-wide cache capacity accounting. Hot and pinned views overlap with resident ownership and must not be added to resident totals. Decoded and pinned capacities use conservative size estimates."
        },
        new("reliability", "Reliability", "events/s", ["ProviderFailure", "SourceFailure", "TransferFailed", "HttpDestinationFailed", "WaitTimeout", "SourceTimeout"])
        {
            Description = "Separate series identify provider/source failures, transfer/destination failures and wait/source timeouts. Rates are local to this node, not fleet totals."
        }
    ];
}