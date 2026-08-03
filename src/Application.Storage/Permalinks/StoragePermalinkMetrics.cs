// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Emits low-cardinality Storage Permalink metrics.
/// </summary>
/// <example>
/// <code>
/// var metrics = new StoragePermalinkMetrics(meterFactory);
/// </code>
/// </example>
public sealed class StoragePermalinkMetrics
{
    /// <summary>
    /// The OpenTelemetry meter name.
    /// </summary>
    public const string MeterName = "BridgingIT.DevKit.Storage.Permalinks";

    private readonly Counter<long> operations;
    private readonly Histogram<double> operationDuration;
    private readonly Counter<long> downloads;
    private readonly Histogram<double> downloadDuration;
    private readonly Counter<long> syncEvents;
    private readonly Histogram<double> syncDuration;
    private readonly Counter<long> syncRetries;
    private long queueDepth;

    /// <summary>
    /// Initializes permalink metrics.
    /// </summary>
    public StoragePermalinkMetrics(IMeterFactory meterFactory = null)
    {
        var meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);
        this.operations = meter.CreateCounter<long>("bdk.storage.permalinks.operations");
        this.operationDuration = meter.CreateHistogram<double>("bdk.storage.permalinks.operation.duration", "ms");
        this.downloads = meter.CreateCounter<long>("bdk.storage.permalinks.downloads");
        this.downloadDuration = meter.CreateHistogram<double>("bdk.storage.permalinks.download.duration", "ms");
        this.syncEvents = meter.CreateCounter<long>("bdk.storage.permalinks.sync.events");
        this.syncDuration = meter.CreateHistogram<double>("bdk.storage.permalinks.sync.duration", "ms");
        this.syncRetries = meter.CreateCounter<long>("bdk.storage.permalinks.sync.retries");
        meter.CreateObservableGauge("bdk.storage.permalinks.sync.queue.depth", () => Interlocked.Read(ref this.queueDepth));
    }

    /// <summary>
    /// Starts an operation timing measurement.
    /// </summary>
    public long Start() => Stopwatch.GetTimestamp();

    /// <summary>
    /// Records one completed registry or maintenance operation.
    /// </summary>
    public void RecordOperation(string operation, long started, IResult result, StorageResourceKind? kind = null, string provider = null) =>
        this.Record(this.operations, this.operationDuration, operation, started, Outcome(result), kind, provider);

    /// <summary>
    /// Records one permalink download request.
    /// </summary>
    public void RecordDownload(long started, string outcome, StorageResourceKind? kind = null)
    {
        var tags = Tags("download", outcome, kind, null);
        this.downloads.Add(1, tags);
        this.downloadDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
    }

    /// <summary>
    /// Records a synchronization event state.
    /// </summary>
    public void RecordSync(StorageResourceChangedNotification notification, string outcome, long? started = null, string provider = null)
    {
        var tags = Tags(notification.ChangeKind.ToString().ToLowerInvariant(), outcome, notification.Location.Kind, provider);
        this.syncEvents.Add(1, tags);
        if (started.HasValue)
        {
            this.syncDuration.Record(Stopwatch.GetElapsedTime(started.Value).TotalMilliseconds, tags);
        }
    }

    /// <summary>
    /// Records a synchronization retry.
    /// </summary>
    public void RecordRetry(StorageResourceChangedNotification notification, string provider = null) =>
        this.syncRetries.Add(1, Tags(notification.ChangeKind.ToString().ToLowerInvariant(), "retry", notification.Location.Kind, provider));

    /// <summary>
    /// Increments the queued-event gauge.
    /// </summary>
    public void IncrementQueueDepth() => Interlocked.Increment(ref this.queueDepth);

    /// <summary>
    /// Decrements the queued-event gauge.
    /// </summary>
    public void DecrementQueueDepth() => Interlocked.Decrement(ref this.queueDepth);

    private void Record(Counter<long> counter, Histogram<double> histogram, string operation, long started, string outcome, StorageResourceKind? kind, string provider)
    {
        var tags = Tags(operation, outcome, kind, provider);
        counter.Add(1, tags);
        histogram.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
    }

    private static TagList Tags(string operation, string outcome, StorageResourceKind? kind, string provider)
    {
        var tags = new TagList { { "operation", operation }, { "outcome", outcome } };
        if (kind.HasValue) tags.Add("storage.kind", kind.Value.ToString().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(provider)) tags.Add("registry.provider", provider.ToLowerInvariant());
        return tags;
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
