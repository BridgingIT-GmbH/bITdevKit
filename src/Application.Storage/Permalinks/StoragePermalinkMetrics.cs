// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits low-cardinality Storage Permalink metrics.
/// </summary>
/// <example>
/// <code>
/// var metrics = new StoragePermalinkMetrics(metricsService);
/// </code>
/// </example>
public sealed class StoragePermalinkMetrics
{
    /// <summary>
    /// The OpenTelemetry meter name.
    /// </summary>
    public const string MeterName = Metrics.MeterName;

    private const string OperationsName = "bdk.storage.permalinks.operations";
    private const string OperationDurationName = "bdk.storage.permalinks.operation.duration";
    private const string DownloadsName = "bdk.storage.permalinks.downloads";
    private const string DownloadDurationName = "bdk.storage.permalinks.download.duration";
    private const string SyncEventsName = "bdk.storage.permalinks.sync.events";
    private const string SyncDurationName = "bdk.storage.permalinks.sync.duration";
    private const string SyncRetriesName = "bdk.storage.permalinks.sync.retries";
    private const string QueueDepthName = "bdk.storage.permalinks.sync.queue.depth";
    private readonly IMetricsService metricsService;
    private long queueDepth;

    /// <summary>
    /// Initializes permalink metrics.
    /// </summary>
    public StoragePermalinkMetrics(IMetricsService metricsService = null)
    {
        this.metricsService = metricsService;
        this.metricsService?.SetGauge(QueueDepthName, 0);
    }

    /// <summary>
    /// Starts an operation timing measurement.
    /// </summary>
    public long Start() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Records one completed registry or maintenance operation.
    /// </summary>
    public void RecordOperation(string operation, long started, IResult result, StorageResourceKind? kind = null, string provider = null) =>
        this.Record(OperationsName, OperationDurationName, operation, started, Outcome(result), kind, provider);

    /// <summary>
    /// Records one permalink download request.
    /// </summary>
    public void RecordDownload(long started, string outcome, StorageResourceKind? kind = null)
    {
        var tags = Tags("download", outcome, kind, null);
        this.metricsService?.AddCounter(DownloadsName, tags: tags);
        this.metricsService?.RecordHistogram(
            DownloadDurationName,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            "ms",
            tags);
    }

    /// <summary>
    /// Records a synchronization event state.
    /// </summary>
    public void RecordSync(StorageResourceChangedNotification notification, string outcome, long? started = null, string provider = null)
    {
        var tags = Tags(notification.ChangeKind.ToString().ToLowerInvariant(), outcome, notification.Location.Kind, provider);
        this.metricsService?.AddCounter(SyncEventsName, tags: tags);
        if (started.HasValue)
        {
            this.metricsService?.RecordHistogram(
                SyncDurationName,
                Stopwatch.GetElapsedTime(started.Value).TotalMilliseconds,
                "ms",
                tags);
        }
    }

    /// <summary>
    /// Records a synchronization retry.
    /// </summary>
    public void RecordRetry(StorageResourceChangedNotification notification, string provider = null) =>
        this.metricsService?.AddCounter(
            SyncRetriesName,
            tags: Tags(notification.ChangeKind.ToString().ToLowerInvariant(), "retry", notification.Location.Kind, provider));

    /// <summary>
    /// Increments the queued-event gauge.
    /// </summary>
    public void IncrementQueueDepth()
    {
        var value = Interlocked.Increment(ref this.queueDepth);
        this.metricsService?.SetGauge(QueueDepthName, value);
    }

    /// <summary>
    /// Decrements the queued-event gauge.
    /// </summary>
    public void DecrementQueueDepth()
    {
        var value = Interlocked.Decrement(ref this.queueDepth);
        this.metricsService?.SetGauge(QueueDepthName, value);
    }

    private void Record(
        string counterName,
        string histogramName,
        string operation,
        long started,
        string outcome,
        StorageResourceKind? kind,
        string provider)
    {
        var tags = Tags(operation, outcome, kind, provider);
        this.metricsService?.AddCounter(counterName, tags: tags);
        this.metricsService?.RecordHistogram(
            histogramName,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            "ms",
            tags);
    }

    private static MetricTag[] Tags(
        string operation,
        string outcome,
        StorageResourceKind? kind,
        string provider)
    {
        var tags = new List<MetricTag>
        {
            new("operation", operation),
            new("outcome", outcome),
        };

        if (kind.HasValue)
        {
            tags.Add(new("storage.kind", kind.Value.ToString().ToLowerInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            tags.Add(new("registry.provider", provider.ToLowerInvariant()));
        }

        return [.. tags];
    }

    private static string Outcome(IResult result) => result switch
    {
        { IsSuccess: true } => "success",
        _ when result.Errors.Any(x => x is StoragePermalinkNotFoundError) => "not_found",
        _ when result.Errors.Any(x => x is StoragePermalinkConflictError) => "conflict",
        _ when result.Errors.Any(x => x is OperationCancelledError) => "cancelled",
        _ => "failure"
    };
}
