// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds asynchronous permalink tracking to one named Document Storage client.
/// </summary>
/// <typeparam name="T">
/// The document type.
/// </typeparam>
/// <example>
/// <code>
/// services.AddDocumentStorage().WithPermalinks&lt;Person&gt;("default");
/// </code>
/// </example>
public sealed class DocumentStorePermalinkBehavior<T>(
    IDocumentStoreClient<T> inner,
    string clientName,
    IStoragePermalinkRegistry registry,
    IStoragePermalinkChangeQueue queue) : DocumentStoreClientBehaviorBase<T>(inner), IStoragePermalinkMoveCoordinator where T : class, new()
{
    private readonly AsyncLocal<int> suppressionDepth = new();
    /// <inheritdoc />
    public string RegistrationName { get; } = $"{typeof(T).FullName?.ToLowerInvariant() ?? typeof(T).Name.ToLowerInvariant()}:{NormalizeName(clientName)}";

    /// <inheritdoc />
    public StorageResourceKind ResourceKind => StorageResourceKind.Document;

    private static string NormalizeName(string value) => string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default)
    {
        var exists = await this.Inner.ExistsAsync(new(location.Scope, location.Path), cancellationToken).ConfigureAwait(false);
        return exists.IsFailure || !exists.Value
            ? Result<StoragePermalinkEntry>.Failure(exists.IsFailure ? exists : Result.Failure(new DocumentStoreNotFoundError()))
            : await registry.GetOrCreateAsync(location, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertAsync(key, value, options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && this.suppressionDepth.Value == 0)
        {
            await queue.EnqueueAsync(new(StorageResourceChangeKind.Upserted, StorageResourceLocation.ForDocument(this.RegistrationName, key))).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertManyAsync(writes, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && this.suppressionDepth.Value == 0)
        {
            foreach (var item in result.Value.Items)
            {
                await queue.EnqueueAsync(new(StorageResourceChangeKind.Upserted, StorageResourceLocation.ForDocument(this.RegistrationName, item.Key))).ConfigureAwait(false);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteAsync(key, options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && this.suppressionDepth.Value == 0)
        {
            await queue.EnqueueAsync(new(StorageResourceChangeKind.Deleted, StorageResourceLocation.ForDocument(this.RegistrationName, key))).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default)
    {
        var result = await base.DeleteManyAsync(deletes, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && this.suppressionDepth.Value == 0)
        {
            foreach (var key in result.Value.Items)
            {
                await queue.EnqueueAsync(new(StorageResourceChangeKind.Deleted, StorageResourceLocation.ForDocument(this.RegistrationName, key))).ConfigureAwait(false);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IDisposable SuppressChangeTracking()
    {
        this.suppressionDepth.Value++;
        return new SuppressionLease(this.suppressionDepth);
    }

    /// <inheritdoc />
    public async Task TrackMoveAsync(StorageResourceLocation source, StorageResourceLocation target) =>
        await queue.EnqueueAsync(new(StorageResourceChangeKind.Moved, source, target)).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task TrackUpsertAsync(StorageResourceLocation target) =>
        await queue.EnqueueAsync(new(StorageResourceChangeKind.Upserted, target)).ConfigureAwait(false);

    private sealed class SuppressionLease(AsyncLocal<int> depth) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (this.disposed) return;
            depth.Value = Math.Max(0, depth.Value - 1);
            this.disposed = true;
        }
    }
}
