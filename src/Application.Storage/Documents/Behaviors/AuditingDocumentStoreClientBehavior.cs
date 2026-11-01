// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Audits selected typed document-client calls without copying document keys, queries, properties, or values.</summary>
/// <typeparam name="T">The document type handled by the client.</typeparam>
/// <param name="inner">The decorated document client.</param>
/// <param name="scopes">The factory resolving optional per-call audit services.</param>
/// <param name="options">Explicit read selection and logical resource projection.</param>
/// <example><code>services.AddDocumentStorage().WithAuditing&lt;Person&gt;();</code></example>
public sealed class AuditingDocumentStoreClientBehavior<T>(IDocumentStoreClient<T> inner, IServiceScopeFactory scopes, StorageAuditOptions options = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    /// <inheritdoc />
    /// <example><code>await documents.GetAsync(new("people", "42"));</code></example>
    public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.AuditAsync("get", () => this.Inner.GetAsync(key, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.GetManyAsync([new("people", "42"), new("people", "43")]);</code></example>
    public override Task<Result<IReadOnlyList<Result<DocumentEntry<T>>>>> GetManyAsync(IReadOnlyList<DocumentKey> keys, CancellationToken cancellationToken = default)
        => this.AuditAsync("get-many", () => this.Inner.GetManyAsync(keys, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.FindPageAsync(new DocumentQuery { AllowFullScan = true });</code></example>
    public override Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.AuditAsync("find-page", () => this.Inner.FindPageAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.ListPageAsync(new DocumentQuery { AllowFullScan = true });</code></example>
    public override Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.AuditAsync("list-page", () => this.Inner.ListPageAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.CountAsync(new DocumentCountQuery { AllowFullScan = true });</code></example>
    public override Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) => this.AuditAsync("count", () => this.Inner.CountAsync(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.ExistsAsync(new("people", "42"));</code></example>
    public override Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.AuditAsync("exists", () => this.Inner.ExistsAsync(key, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.UpsertAsync(new("people", "42"), new T());</code></example>
    public override Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) => this.AuditAsync("upsert", () => this.Inner.UpsertAsync(key, value, options, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.UpsertManyAsync([new(new("people", "42"), new T())]);</code></example>
    public override Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default) => this.AuditAsync("upsert-many", () => this.Inner.UpsertManyAsync(writes, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.UpdatePropertiesAsync(new(new("people", "42")));</code></example>
    public override Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) => this.AuditAsync("update-properties", () => this.Inner.UpdatePropertiesAsync(update, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.DeleteAsync(new("people", "42"));</code></example>
    public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.AuditAsync("delete", () => this.Inner.DeleteAsync(key, options, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await documents.DeleteManyAsync([new(new("people", "42"))]);</code></example>
    public override Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default) => this.AuditAsync("delete-many", () => this.Inner.DeleteManyAsync(deletes, cancellationToken), cancellationToken);

    private Task<TResult> AuditAsync<TResult>(string operation, Func<Task<TResult>> next, CancellationToken cancellationToken) where TResult : IResult
        => StorageAuditBehaviorExecutor.RunAsync(scopes, "DocumentStorage", operation, () => (this.Inner as IDocumentStoreClientIdentity)?.ClientName ?? "default",
            operation is "get" or "get-many" or "find-page" or "list-page" or "count" or "exists", options, next, (scope, result) =>
            {
                var partial = result is Result<DocumentBatchResult<DocumentInfo>> { IsSuccess: true, Value: { } writes } && (writes.FailedKey is not null || writes.FailedKeys?.Count > 0)
                    || result is Result<DocumentBatchResult<DocumentKey>> { IsSuccess: true, Value: { } deletes } && (deletes.FailedKey is not null || deletes.FailedKeys?.Count > 0)
                    || result is Result<IReadOnlyList<Result<DocumentEntry<T>>>> { IsSuccess: true, Value: { } reads } && reads.Any(item => item.IsFailure);
                if (partial)
                {
                    scope.Complete(AuditOutcome.Failed);
                    (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "PartialBatch" });
                }
            });
}
