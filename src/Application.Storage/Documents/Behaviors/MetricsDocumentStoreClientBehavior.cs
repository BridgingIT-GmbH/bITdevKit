// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

/// <summary>Emits low-cardinality operation counts and durations.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new MetricsDocumentStoreClientBehavior&lt;Person&gt;(inner, metricsService);</code></example>
public sealed class MetricsDocumentStoreClientBehavior<T> : DocumentStoreClientBehaviorBase<T> where T : class, new()
{
    private readonly IMetricsService metricsService;

    /// <summary>Initializes the metrics behavior.</summary>
    public MetricsDocumentStoreClientBehavior(
        IDocumentStoreClient<T> inner,
        IMetricsService metricsService = null) : base(inner)
    {
        this.metricsService = metricsService;
    }

    /// <inheritdoc />
    public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.Measure("get", () => base.GetAsync(key, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.Measure("find", () => base.FindPageAsync(query, cancellationToken));
    /// <inheritdoc />
    public override Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) => this.Measure("upsert", () => base.UpsertAsync(key, value, options, cancellationToken));
    /// <inheritdoc />
    public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.Measure("delete", () => base.DeleteAsync(key, options, cancellationToken));

    private async Task<TResult> Measure<TResult>(string operation, Func<Task<TResult>> action)
    {
        if (this.metricsService is null)
        {
            return await action().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        try { return await action().ConfigureAwait(false); }
        finally
        {
            MetricTag[] tags = [new("operation", operation), new("document.type", typeof(T).Name)];
            this.metricsService.AddCounter("document.operations", tags: tags);
            this.metricsService.RecordHistogram(
                "document.operation.duration",
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                "ms",
                tags);
        }
    }
}
