// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits low-cardinality operation counts, durations, failures, bytes, and transfer rates for every document operation.
/// </summary>
/// <remarks>
/// Instruments (tags <c>operation</c> and <c>document.type</c>):
/// <c>document.operations</c>, <c>document.operation.duration</c> (ms), <c>document.operation.failures</c>,
/// <c>document.bytes</c> (logical serialized bytes read or written), and <c>document.bytes_per_second</c> (<c>By/s</c>,
/// recorded for successful non-empty reads and writes only). A batch that stops at a failed key counts as a failure.
/// </remarks>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new MetricsDocumentStoreClientBehavior&lt;Person&gt;(inner, metricsService);</code></example>
public sealed class MetricsDocumentStoreClientBehavior<T> : DocumentStoreClientBehaviorBase<T> where T : class, new()
{
    private readonly IMetricsService metricsService;

    /// <summary>Initializes the metrics behavior.</summary>
    /// <param name="inner">The decorated document client.</param>
    /// <param name="metricsService">The optional shared metrics service.</param>
    /// <example><code>var behavior = new MetricsDocumentStoreClientBehavior&lt;Person&gt;(inner, metricsService);</code></example>
    public MetricsDocumentStoreClientBehavior(
        IDocumentStoreClient<T> inner,
        IMetricsService metricsService = null) : base(inner)
    {
        this.metricsService = metricsService;
    }

    /// <inheritdoc />
    public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.Measure("get", () => base.GetAsync(key, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<IReadOnlyList<Result<DocumentEntry<T>>>>> GetManyAsync(IReadOnlyList<DocumentKey> keys, CancellationToken cancellationToken = default) => this.Measure("get-many", () => base.GetManyAsync(keys, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.Measure("find", () => base.FindPageAsync(query, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.Measure("list", () => base.ListPageAsync(query, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) => this.Measure("count", () => base.CountAsync(query, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.Measure("exists", () => base.ExistsAsync(key, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) => this.Measure("upsert", () => base.UpsertAsync(key, value, options, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default) => this.Measure("upsert-many", () => base.UpsertManyAsync(writes, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) => this.Measure("update-properties", () => base.UpdatePropertiesAsync(update, cancellationToken));
    /// <inheritdoc />
    public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.Measure("delete", () => base.DeleteAsync(key, options, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default) => this.Measure("delete-many", () => base.DeleteManyAsync(deletes, cancellationToken));

    private async Task<TResult> Measure<TResult>(string operation, Func<Task<TResult>> action)
        where TResult : IResult
    {
        if (this.metricsService is null)
        {
            return await action().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        IResult recorded = null;
        try
        {
            var result = await action().ConfigureAwait(false);
            recorded = result;

            return result;
        }
        finally
        {
            this.Record(operation, started, recorded);
        }
    }

    private void Record(string operation, long started, IResult result)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        MetricTag[] tags = [new("operation", operation), new("document.type", typeof(T).Name)];
        this.metricsService.AddCounter("document.operations", tags: tags);
        this.metricsService.RecordHistogram("document.operation.duration", elapsed.TotalMilliseconds, "ms", tags);

        // A null result means the call threw or was cancelled.
        if (result is null || result.IsFailure || HasFailedKey(result))
        {
            this.metricsService.AddCounter("document.operation.failures", tags: tags);
        }

        if (result is null || result.IsFailure)
        {
            return;
        }

        var bytes = GetBytes(result);
        if (bytes <= 0)
        {
            return;
        }

        this.metricsService.AddCounter("document.bytes", bytes, tags);
        if (IsTransfer(operation) && elapsed > TimeSpan.Zero)
        {
            this.metricsService.RecordHistogram("document.bytes_per_second", bytes / elapsed.TotalSeconds, "By/s", tags);
        }
    }

    private static bool IsTransfer(string operation) =>
        operation is "get" or "get-many" or "find" or "upsert" or "upsert-many";

    private static bool HasFailedKey(IResult result) =>
        result is Result<DocumentBatchResult<DocumentInfo>> { IsSuccess: true, Value.FailedKey: not null } ||
        result is Result<DocumentBatchResult<DocumentKey>> { IsSuccess: true, Value.FailedKey: not null };

    private static long GetBytes(IResult result) => result switch
    {
        Result<DocumentEntry<T>> { IsSuccess: true } entry => entry.Value.ContentLength,
        Result<IReadOnlyList<Result<DocumentEntry<T>>>> { IsSuccess: true } many => many.Value.Where(x => x.IsSuccess).Sum(x => x.Value.ContentLength),
        Result<DocumentInfo> { IsSuccess: true } info => info.Value.ContentLength,
        Result<DocumentPage<T>> { IsSuccess: true } page => page.Value.Items.Sum(x => x.ContentLength),
        Result<DocumentBatchResult<DocumentInfo>> { IsSuccess: true } batch => batch.Value.Items.Sum(x => x.ContentLength),
        _ => 0
    };
}
