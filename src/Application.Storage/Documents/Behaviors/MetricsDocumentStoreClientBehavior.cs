// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>Emits low-cardinality operation counts and durations.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new MetricsDocumentStoreClientBehavior&lt;Person&gt;(meterFactory, inner);</code></example>
public sealed class MetricsDocumentStoreClientBehavior<T> : DocumentStoreClientBehaviorBase<T> where T : class, new()
{
    private readonly Counter<long> operations;
    private readonly Histogram<double> duration;

    /// <summary>Initializes the metrics behavior.</summary>
    public MetricsDocumentStoreClientBehavior(IMeterFactory meterFactory, IDocumentStoreClient<T> inner) : base(inner)
    {
        var meter = meterFactory?.Create("BridgingIT.DevKit.DocumentStorage") ?? new Meter("BridgingIT.DevKit.DocumentStorage");
        this.operations = meter.CreateCounter<long>("document.operations");
        this.duration = meter.CreateHistogram<double>("document.operation.duration", "ms");
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
        var started = Stopwatch.GetTimestamp();
        try { return await action(); }
        finally
        {
            var tags = new TagList { { "operation", operation }, { "document.type", typeof(T).Name } };
            this.operations.Add(1, tags);
            this.duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        }
    }
}
