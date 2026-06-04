// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides shared metadata for metrics snapshot responses.
/// </summary>
/// <example>
/// <code>
/// var snapshot = await httpClient.GetFromJsonAsync&lt;BdkMetricsSnapshotModel&gt;(\"/api/_system/metrics/bdk\");
/// var capturedAt = snapshot.CapturedAtUtc;
/// </code>
/// </example>
public abstract class MetricsSnapshotBase
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the snapshot was captured.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the current process started.
    /// </summary>
    public DateTimeOffset ProcessStartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the process uptime in seconds at capture time.
    /// </summary>
    public double UptimeSeconds { get; set; }
}

/// <summary>
/// Represents a named numeric metric value.
/// </summary>
public class MetricValueModel
{
    /// <summary>
    /// Gets or sets the metric series name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the metric value.
    /// </summary>
    public double Value { get; set; }
}

/// <summary>
/// Represents a latency summary for one metric series.
/// </summary>
public class MetricLatencyModel
{
    /// <summary>
    /// Gets or sets the latency series name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of recorded measurements.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the cumulative sum of recorded values.
    /// </summary>
    public double Sum { get; set; }

    /// <summary>
    /// Gets or sets the average value.
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public double Max { get; set; }
}

/// <summary>
/// Represents an aggregated histogram snapshot for one devkit duration series.
/// </summary>
public class BdkHistogramSnapshotModel
{
    /// <summary>
    /// Gets or sets the number of histogram measurements.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the cumulative sum of measurements.
    /// </summary>
    public double Sum { get; set; }

    /// <summary>
    /// Gets or sets the average measurement value.
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Gets or sets the maximum measurement value.
    /// </summary>
    public double Max { get; set; }
}

/// <summary>
/// Represents the grouped devkit metrics for one feature area.
/// </summary>
public class BdkFeatureSnapshotModel
{
    /// <summary>
    /// Gets or sets the feature group name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the raw cumulative counter values.
    /// </summary>
    public Dictionary<string, double> Counters { get; set; } = [];

    /// <summary>
    /// Gets or sets the raw current live-view values.
    /// </summary>
    public Dictionary<string, double> Current { get; set; } = [];

    /// <summary>
    /// Gets or sets the duration histograms.
    /// </summary>
    public Dictionary<string, BdkHistogramSnapshotModel> Durations { get; set; } = [];

    /// <summary>
    /// Gets or sets the summed cumulative success total for the feature.
    /// </summary>
    public double SuccessTotal { get; set; }

    /// <summary>
    /// Gets or sets the summed cumulative failure total for the feature.
    /// </summary>
    public double FailureTotal { get; set; }

    /// <summary>
    /// Gets or sets the summed current live-view total for the feature.
    /// </summary>
    public double CurrentTotal { get; set; }

    /// <summary>
    /// Gets or sets the busiest cumulative series for the feature.
    /// </summary>
    public List<MetricValueModel> TopThroughput { get; set; } = [];

    /// <summary>
    /// Gets or sets the busiest current live-view series for the feature.
    /// </summary>
    public List<MetricValueModel> TopCurrent { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest failure series for the feature.
    /// </summary>
    public List<MetricValueModel> TopFailures { get; set; } = [];

    /// <summary>
    /// Gets or sets the latency highlight series for the feature.
    /// </summary>
    public List<MetricLatencyModel> LatencyHighlights { get; set; } = [];
}

/// <summary>
/// Represents the full live snapshot of the devkit meter grouped by feature.
/// </summary>
/// <example>
/// <code>
/// var snapshot = await httpClient.GetFromJsonAsync&lt;BdkMetricsSnapshotModel&gt;(\"/api/_system/metrics/bdk\");
/// var currentQueueWork = snapshot.Features[\"queueing\"].CurrentTotal;
/// </code>
/// </example>
public class MetricsSnapshotModel : MetricsSnapshotBase
{
    /// <summary>
    /// Gets or sets the source meter name.
    /// </summary>
    public string Meter { get; set; } = BridgingIT.DevKit.Common.Metrics.MeterName;

    /// <summary>
    /// Gets or sets the feature groups in the snapshot.
    /// </summary>
    public Dictionary<string, BdkFeatureSnapshotModel> Features { get; set; } = [];

    /// <summary>
    /// Gets or sets the catalog of currently available devkit metrics for drill-down dashboards.
    /// </summary>
    public List<BdkMetricDescriptorModel> AvailableMetrics { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest failure series across the snapshot.
    /// </summary>
    public List<MetricValueModel> TopFailures { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest cumulative throughput series across the snapshot.
    /// </summary>
    public List<MetricValueModel> TopThroughput { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest current live-view series across the snapshot.
    /// </summary>
    public List<MetricValueModel> TopCurrent { get; set; } = [];

    /// <summary>
    /// Gets or sets the latency highlights across the snapshot.
    /// </summary>
    public List<MetricLatencyModel> LatencyHighlights { get; set; } = [];
}

/// <summary>
/// Describes one available devkit metric series for dashboard drill-down selection.
/// </summary>
public class BdkMetricDescriptorModel
{
    /// <summary>
    /// Gets or sets the metric series name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the feature group name.
    /// </summary>
    public string Feature { get; set; }

    /// <summary>
    /// Gets or sets the metric kind, for example <c>total</c>, <c>current</c>, or <c>duration</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the metric unit, if any.
    /// </summary>
    public string Unit { get; set; }
}

/// <summary>
/// Represents an overview projection for a single feature area.
/// </summary>
public class MetricsOverviewFeatureModel
{
    /// <summary>
    /// Gets or sets the feature name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the cumulative success total.
    /// </summary>
    public double SuccessTotal { get; set; }

    /// <summary>
    /// Gets or sets the cumulative failure total.
    /// </summary>
    public double FailureTotal { get; set; }

    /// <summary>
    /// Gets or sets the current live-view total.
    /// </summary>
    public double CurrentTotal { get; set; }

    /// <summary>
    /// Gets or sets the busiest cumulative throughput series.
    /// </summary>
    public MetricValueModel TopThroughput { get; set; }

    /// <summary>
    /// Gets or sets the busiest current live-view series.
    /// </summary>
    public MetricValueModel TopCurrent { get; set; }

    /// <summary>
    /// Gets or sets the slowest latency series.
    /// </summary>
    public MetricLatencyModel SlowestSeries { get; set; }
}

/// <summary>
/// Represents the dashboard-oriented metrics overview response.
/// </summary>
/// <example>
/// <code>
/// var overview = await httpClient.GetFromJsonAsync&lt;MetricsOverviewSnapshotModel&gt;(\"/api/_system/metrics/overview\");
/// var currentWork = overview.SummaryCards[\"totalCurrent\"];
/// </code>
/// </example>
public class MetricsOverviewSnapshotModel : MetricsSnapshotBase
{
    /// <summary>
    /// Gets or sets the summary cards shown at the overview level.
    /// </summary>
    public Dictionary<string, double> SummaryCards { get; set; } = [];

    /// <summary>
    /// Gets or sets the per-feature overview projections.
    /// </summary>
    public Dictionary<string, MetricsOverviewFeatureModel> Features { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest failure series across the overview.
    /// </summary>
    public List<MetricValueModel> TopFailures { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest cumulative throughput series across the overview.
    /// </summary>
    public List<MetricValueModel> TopThroughput { get; set; } = [];

    /// <summary>
    /// Gets or sets the highest current live-view series across the overview.
    /// </summary>
    public List<MetricValueModel> TopCurrent { get; set; } = [];

    /// <summary>
    /// Gets or sets the latency highlights across the overview.
    /// </summary>
    public List<MetricLatencyModel> LatencyHighlights { get; set; } = [];
}

/// <summary>
/// Represents a curated snapshot of useful .NET runtime metrics.
/// </summary>
public class DotNetMetricsSnapshotModel : MetricsSnapshotBase
{
    public double CpuUsagePercent { get; set; }

    public double WorkingSetMb { get; set; }

    public double PrivateMemoryMb { get; set; }

    public double ManagedMemoryMb { get; set; }

    public double HeapSizeMb { get; set; }

    public double FragmentedMemoryMb { get; set; }

    public double TotalAllocatedMb { get; set; }

    public long Gen0Collections { get; set; }

    public long Gen1Collections { get; set; }

    public long Gen2Collections { get; set; }

    public int ThreadCount { get; set; }

    public int WorkerThreadsUsed { get; set; }

    public int WorkerThreadsAvailable { get; set; }

    public int IoThreadsUsed { get; set; }

    public int IoThreadsAvailable { get; set; }

    public long PendingWorkItems { get; set; }
}

/// <summary>
/// Represents a curated snapshot of useful ASP.NET request metrics.
/// </summary>
public class AspNetMetricsSnapshotModel : MetricsSnapshotBase
{
    public long TotalRequests { get; set; }

    public long TrackedRouteCount { get; set; }

    public long ActiveRequests { get; set; }

    public long MaxObservedConcurrentRequests { get; set; }

    public long FailedRequests { get; set; }

    public double FailureRatePercent { get; set; }

    public double AverageLatencyMs { get; set; }

    public long TotalLatencyMs { get; set; }

    public double RequestsPerMinute { get; set; }

    public long Status1xx { get; set; }

    public long Status2xx { get; set; }

    public long Status3xx { get; set; }

    public long Status4xx { get; set; }

    public long Status5xx { get; set; }

    public DateTimeOffset? LastRequestAtUtc { get; set; }
}

/// <summary>
/// Represents one aggregated ASP.NET route entry for operations dashboards.
/// </summary>
/// <example>
/// <code>
/// var snapshot = await httpClient.GetFromJsonAsync&lt;AspNetRouteMetricsSnapshotModel&gt;("/api/_system/metrics/aspnet/routes");
/// var slowest = snapshot.Routes.FirstOrDefault();
/// </code>
/// </example>
public class AspNetRouteMetricsSnapshotModel : MetricsSnapshotBase
{
    /// <summary>
    /// Gets or sets the total number of tracked route entries in the snapshot.
    /// </summary>
    public long TrackedRouteCount { get; set; }

    /// <summary>
    /// Gets or sets the per-route metrics rows.
    /// </summary>
    public List<AspNetRouteMetricsModel> Routes { get; set; } = [];
}

/// <summary>
/// Represents one per-route ASP.NET metrics row.
/// </summary>
public class AspNetRouteMetricsModel
{
    /// <summary>
    /// Gets or sets the HTTP method.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the normalized route template or path.
    /// </summary>
    public string Route { get; set; }

    /// <summary>
    /// Gets or sets the total number of captured requests for the route.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the count of informational responses.
    /// </summary>
    public long Status1xx { get; set; }

    /// <summary>
    /// Gets or sets the count of successful responses.
    /// </summary>
    public long Status2xx { get; set; }

    /// <summary>
    /// Gets or sets the count of redirection responses.
    /// </summary>
    public long Status3xx { get; set; }

    /// <summary>
    /// Gets or sets the count of client-error responses.
    /// </summary>
    public long Status4xx { get; set; }

    /// <summary>
    /// Gets or sets the count of server-error responses.
    /// </summary>
    public long Status5xx { get; set; }

    /// <summary>
    /// Gets or sets the count of failed requests for the route.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the failure rate percentage for the route.
    /// </summary>
    public double FailureRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the cumulative latency total in milliseconds for the route.
    /// </summary>
    public long TotalLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the average request latency in milliseconds for the route.
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent captured request for the route.
    /// </summary>
    public DateTimeOffset? LastRequestAtUtc { get; set; }
}
