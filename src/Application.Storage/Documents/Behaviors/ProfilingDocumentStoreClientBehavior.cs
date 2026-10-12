// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Profiles every typed document-client call using named client, document type and operation dimensions.</summary>
/// <typeparam name="T">The document type handled by the client.</typeparam>
/// <param name="inner">The decorated document client.</param>
/// <param name="profiling">The optional shared operation profiler.</param>
/// <example><code>services.AddDocumentStorage().WithProfilingBehavior&lt;Person&gt;();</code></example>
public sealed class ProfilingDocumentStoreClientBehavior<T>(IDocumentStoreClient<T> inner, IOperationProfiler profiling = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    /// <inheritdoc />
    /// <example><code>await documents.GetAsync(new("people", "42"));</code></example>
    public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.ProfileAsync("get", () => this.Inner.GetAsync(key, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.FindPageAsync(new DocumentQuery { AllowFullScan = true });</code></example>
    public override Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.ProfileAsync("find-page", () => this.Inner.FindPageAsync(query, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.ListPageAsync(new DocumentQuery { AllowFullScan = true });</code></example>
    public override Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.ProfileAsync("list-page", () => this.Inner.ListPageAsync(query, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.CountAsync(new DocumentCountQuery { AllowFullScan = true });</code></example>
    public override Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) => this.ProfileAsync("count", () => this.Inner.CountAsync(query, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.ExistsAsync(new("people", "42"));</code></example>
    public override Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.ProfileAsync("exists", () => this.Inner.ExistsAsync(key, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.UpsertAsync(new("people", "42"), new T());</code></example>
    public override Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) => this.ProfileAsync("upsert", () => this.Inner.UpsertAsync(key, value, options, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.UpsertManyAsync([new(new("people", "42"), new T())]);</code></example>
    public override Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default) => this.ProfileAsync("upsert-many", () => this.Inner.UpsertManyAsync(writes, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.UpdatePropertiesAsync(new(new("people", "42")));</code></example>
    public override Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) => this.ProfileAsync("update-properties", () => this.Inner.UpdatePropertiesAsync(update, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.DeleteAsync(new("people", "42"));</code></example>
    public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.ProfileAsync("delete", () => this.Inner.DeleteAsync(key, options, cancellationToken), cancellationToken);
    /// <inheritdoc />
    /// <example><code>await documents.DeleteManyAsync([new(new("people", "42"))]);</code></example>
    public override Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default) => this.ProfileAsync("delete-many", () => this.Inner.DeleteManyAsync(deletes, cancellationToken), cancellationToken);

    private Task<TResult> ProfileAsync<TResult>(string operation, Func<Task<TResult>> next, CancellationToken cancellationToken)
        where TResult : IResult
    {
        if (profiling is null) { return next(); }

        return profiling.JoinOrStartAsync(new OperationProfilingStartRequest
        {
            Key = "documentstorage:" + typeof(T).Name + ":" + operation, Kind = "DocumentStorage", CorrelationId = CorrelationId.Current,
        }, (scope, _) =>
        {
            try
            {
                if (scope.IsRecording)
                {
                    scope.SetDimension("documentstorage.client", (this.Inner as IDocumentStoreClientIdentity)?.ClientName ?? "default");
                    scope.SetDimension("documentstorage.documentType", typeof(T).FullName);
                    scope.SetDimension("documentstorage.operation", operation);
                }
            }
            catch (Exception) { /* No document keys, queries or payloads are retained. */ }

            return next();
        }, cancellationToken, Classify);
    }

    private static ProfilingResultClassification Classify<TResult>(TResult result) where TResult : IResult
    {
        var partial = result is Result<DocumentBatchResult<DocumentInfo>> { IsSuccess: true, Value: { } writes }
                && (writes.FailedKey is not null || writes.FailedKeys.Count > 0)
            || result is Result<DocumentBatchResult<DocumentKey>> { IsSuccess: true, Value: { } deletes }
                && (deletes.FailedKey is not null || deletes.FailedKeys.Count > 0);
        return partial ? new() { Outcome = OperationProfilingOutcome.Failed, Failure = new() { Source = "DocumentStorage", Code = "PartialBatch" } }
            : OperationProfilingHelpers.ClassifyResult(result);
    }
}
