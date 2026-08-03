// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds asynchronous permalink tracking to one named Blob Storage client.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage().WithPermalinks("reports");
/// </code>
/// </example>
public sealed class BlobStorePermalinkBehavior(
    IBlobStoreClient inner,
    string storeName,
    IStoragePermalinkRegistry registry,
    IStoragePermalinkChangeQueue queue) : BlobStoreClientBehaviorBase(inner, storeName), IStoragePermalinkMoveCoordinator
{
    private readonly AsyncLocal<int> suppressionDepth = new();
    /// <inheritdoc />
    public string RegistrationName { get; } = string.IsNullOrWhiteSpace(storeName) ? "default" : storeName.Trim().ToLowerInvariant();

    /// <inheritdoc />
    public StorageResourceKind ResourceKind => StorageResourceKind.Blob;

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default)
    {
        var key = new BlobKey(location.Scope, location.Path);
        var exists = await this.Inner.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
        return exists.IsFailure
            ? Result<StoragePermalinkEntry>.Failure(exists)
            : await registry.GetOrCreateAsync(location, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task<Result<T>> ExecuteAsync<T>(string operation, BlobStoreOperationContext context, Func<CancellationToken, Task<Result<T>>> next, CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && operation == "upload" && context.Key is not null && this.suppressionDepth.Value == 0)
        {
            await queue.EnqueueAsync(new(StorageResourceChangeKind.Upserted, StorageResourceLocation.ForBlob(this.RegistrationName, context.Key))).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    protected override async Task<Result> ExecuteAsync(string operation, BlobStoreOperationContext context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && operation == "delete" && context.Key is not null && this.suppressionDepth.Value == 0)
        {
            await queue.EnqueueAsync(new(StorageResourceChangeKind.Deleted, StorageResourceLocation.ForBlob(this.RegistrationName, context.Key))).ConfigureAwait(false);
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
