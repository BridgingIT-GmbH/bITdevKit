// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using Microsoft.EntityFrameworkCore.Storage;

public sealed partial class EntityFrameworkBlobStoreProvider<TContext> : IBlobDimensionDiagnosticsProvider where TContext : DbContext, IBlobStoreContext
{
    /// <inheritdoc />
    public async Task<Result<BlobDimensionDiagnostics>> GetDimensionDiagnosticsAsync(bool includeDictionaryCount = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.Capabilities.Dimensions.SupportsDimensions) return Result<BlobDimensionDiagnostics>.Failure(new BlobDimensionsNotSupportedError());
        var snapshot = this.dimensionNames.Capture();
        if (!includeDictionaryCount) return Result<BlobDimensionDiagnostics>.Success(snapshot);
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var count = await context.Set<StorageBlobDimensionName>().AsNoTracking().LongCountAsync(cancellationToken).ConfigureAwait(false);
            return Result<BlobDimensionDiagnostics>.Success(snapshot with { DictionaryEntries = count });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Result<BlobDimensionDiagnostics>.Failure(new BlobStoreProviderError("Dimension dictionary inspection failed.", SqlServerBlobErrors.IsTransient(exception)));
        }
    }

    /// <summary>Clears cached dictionary identities after external resource recreation.</summary>
    /// <remarks>Call when reusing this provider instance after recreating its database or Dimensions tables. This operation performs no database I/O.</remarks>
    /// <example><code>provider.ClearDimensionNameCache();</code></example>
    public void ClearDimensionNameCache() => this.dimensionNames.Clear();

    /// <inheritdoc />
    public Task<Result<StoredBlobInfo>> UpdateDimensionsAsync(
        BlobDimensionsUpdate update,
        CancellationToken cancellationToken = default) =>
        this.UpdateDimensionsAsync(update, null, cancellationToken);

    private async Task<Result<StoredBlobInfo>> UpdateDimensionsAsync(
        BlobDimensionsUpdate update,
        StoredBlobPropertiesUpdate propertiesUpdate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!this.Capabilities.Dimensions.SupportsDimensions) return Result<StoredBlobInfo>.Failure(new BlobDimensionsNotSupportedError());
        if (update?.Dimensions is null) return Result<StoredBlobInfo>.Failure(new BlobStoreValidationError("Dimensions input is required."));
        var validation = BlobValidator.Validate(update.Key);
        if (validation.IsSuccess) validation = BlobValidator.Validate(new BlobDeleteOptions { IfMatchETag = update.IfMatchETag });
        if (validation.IsFailure)
        {
            return Result<StoredBlobInfo>.Failure(validation);
        }

        await using var lease = this.CreateContextLease();
        var dbContext = lease.Context;
        using var mutationGate = await AcquireMutationGateAsync(dbContext, update.Key, cancellationToken).ConfigureAwait(false);
        await using var dimensions = this.dimensionNames.BeginSession();
        StorageBlob snapshot = null;
        BlobMutationLease mutationLease = null;
        IDbContextTransaction transaction = null;

        try
        {
            transaction = await this.BeginTransactionIfSupportedAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var identity = CreateKeyIdentity(update.Key);
            var blob = await this.QueryExactBlob(dbContext, identity).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (blob is null || IsExpired(blob, this.timeProvider.GetUtcNow()))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<StoredBlobInfo>.Failure(new BlobStoreNotFoundError(update.Key));
            }

            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) &&
                !string.Equals(update.IfMatchETag, blob.ETag, StringComparison.Ordinal))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<StoredBlobInfo>.Failure(new BlobStoreConflictError(
                    $"Blob ETag does not match for '{update.Key.Container}/{update.Key.Name}'."));
            }

            if (!await this.TryAcquireLeaseAsync(dbContext, blob, cancellationToken, reload: false).ConfigureAwait(false))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(update.IfMatchETag))
                {
                    var latest = await this.QueryExactBlob(dbContext, identity).AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                    if (latest is null || latest.ETag != update.IfMatchETag)
                        return Result<StoredBlobInfo>.Failure(new BlobStoreConflictError("The blob ETag changed."));
                }

                return Result<StoredBlobInfo>.Failure(new BlobStoreLeaseError(
                    $"Could not acquire blob property update lease for '{update.Key.Container}/{update.Key.Name}'."));
            }

            mutationLease = new(blob.Id, blob.LeaseId, blob.ConcurrencyVersion);
            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) && blob.ETag != update.IfMatchETag)
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<StoredBlobInfo>.Failure(new BlobStoreConflictError("The blob ETag changed."));
            }

            snapshot = CloneBlob(blob);
            var changed = await BlobDimensionMutations.ApplyAsync(dbContext, blob, update.Dimensions, this.dimensionNames, dimensions, cancellationToken).ConfigureAwait(false);
            if (changed.IsFailure) throw new BlobStoreResultException(changed.Errors.First());
            if (propertiesUpdate is not null)
            {
                ApplyPropertyUpdate(blob, propertiesUpdate);
            }

            blob.ETag = CreateETag();
            blob.LastModifiedAt = this.timeProvider.GetUtcNow();
            blob.LeaseId = null;
            blob.LeaseAcquiredBy = null;
            blob.LeaseAcquiredUntil = null;
            blob.ConcurrencyVersion = Guid.NewGuid();

            // This operation aborts the complete owned transaction on any failure; registration contexts retain savepoints.
            var savepoints = dbContext.Database.AutoSavepointsEnabled;
            dbContext.Database.AutoSavepointsEnabled = false;
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                dbContext.Database.AutoSavepointsEnabled = savepoints;
            }

            if (transaction is not null)
            {
                var operation = propertiesUpdate is null ? "update-dimensions" : "update-properties";
                var committed = await CommitBlobMutationAsync(transaction, operation, cancellationToken).ConfigureAwait(false);
                if (committed.IsFailure) return Result<StoredBlobInfo>.Failure(committed);
            }

            this.dimensionNames.PublishCommitted(dimensions);
            return Result<StoredBlobInfo>.Success(ToInfo(blob));
        }
        catch (BlobStoreResultException exception)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<StoredBlobInfo>.Failure(exception.Error);
        }
        catch (DbUpdateConcurrencyException)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<StoredBlobInfo>.Failure(new BlobStoreConflictError("The blob ETag changed."));
        }
        catch (OperationCanceledException)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreMetadataSnapshotIfNeededAsync(dbContext, snapshot, mutationLease, transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreMetadataSnapshotIfNeededAsync(dbContext, snapshot, mutationLease, transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<StoredBlobInfo>.Failure(SqlServerBlobErrors.ProviderError(exception));
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<Result> CommitBlobMutationAsync(IDbContextTransaction transaction, string operation, CancellationToken token)
    {
        try
        {
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Result.Failure(new BlobStoreIndeterminateOperationError(operation, transaction.TransactionId.ToString("N")));
        }
    }

    private static BlobDimensions ReadDimensions(string json)
    {
        if (json is null) return BlobDimensions.Empty;
        var result = BlobDimensionValues.Deserialize(json);
        return result.IsSuccess ? result.Value : throw new InvalidDataException("Stored blob Dimensions snapshot is invalid.");
    }
}
