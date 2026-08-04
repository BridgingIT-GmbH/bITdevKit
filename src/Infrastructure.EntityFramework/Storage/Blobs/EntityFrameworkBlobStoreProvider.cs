// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Storage;
using System.Buffers;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Provides the Entity Framework blob-store provider.
/// </summary>
/// <remarks>
/// Resolves operation-scoped <typeparamref name="TContext" /> instances and persists blob metadata and content chunks
/// through EF Core.
/// </remarks>
/// <typeparam name="TContext">The EF Core context type that implements <see cref="IBlobStoreContext" />.</typeparam>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithEntityFrameworkClient&lt;AppDbContext&gt;("reports");
/// </code>
/// </example>
public sealed partial class EntityFrameworkBlobStoreProvider<TContext> : IBlobStoreProvider, IBlobStoreRetentionProvider, IBlobStoreContainerCatalog
    where TContext : DbContext, IBlobStoreContext
{
    /// <summary>
    /// Defines the provider name used for diagnostics and continuation-token binding.
    /// </summary>
    public const string ProviderName = "Entity Framework";

    private readonly IServiceScopeFactory scopeFactory;
    private readonly BlobStoreOptions options;
    private readonly IContinuationTokenProtector continuationTokenProtector;
    private readonly Counter<long> chunksWritten;
    private readonly Counter<long> chunkFlushes;
    private readonly Histogram<long> chunksPerFlush;
    private readonly Histogram<long> bytesPerFlush;
    private readonly ILogger logger;
    private readonly string storeName;
    private readonly string leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkBlobStoreProvider{TContext}" /> class for
    /// DI-managed usage with fresh scoped contexts per operation.
    /// </summary>
    /// <param name="scopeFactory">The root scope factory used to create provider-owned operation contexts.</param>
    /// <param name="options">The blob-store options.</param>
    /// <param name="continuationTokenProtector">The optional continuation-token protector.</param>
    /// <param name="meterFactory">The optional meter factory for chunk-flush metrics.</param>
    /// <param name="loggerFactory">The optional logger factory for chunk-flush diagnostics.</param>
    /// <param name="storeName">The low-cardinality named-store identifier.</param>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkBlobStoreProvider&lt;AppDbContext&gt;(scopeFactory);
    /// </code>
    /// </example>
    public EntityFrameworkBlobStoreProvider(
        IServiceScopeFactory scopeFactory,
        BlobStoreOptions options = null,
        IContinuationTokenProtector continuationTokenProtector = null,
        IMeterFactory meterFactory = null,
        ILoggerFactory loggerFactory = null,
        string storeName = null)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.options = options ?? new BlobStoreOptions();
        this.continuationTokenProtector = continuationTokenProtector;
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<EntityFrameworkBlobStoreProvider<TContext>>();
        this.storeName = string.IsNullOrWhiteSpace(storeName) ? "default" : storeName;
        var meter = meterFactory?.Create(Metrics.MeterName);
        this.chunksWritten = meter?.CreateCounter<long>("blobstorage_ef_chunks_written");
        this.chunkFlushes = meter?.CreateCounter<long>("blobstorage_ef_chunk_flushes");
        this.chunksPerFlush = meter?.CreateHistogram<long>(
            "blobstorage_ef_chunks_per_flush",
            unit: "{chunk}");
        this.bytesPerFlush = meter?.CreateHistogram<long>(
            "blobstorage_ef_bytes_per_flush",
            unit: "By");
    }

    /// <inheritdoc />
    public BlobStoreProviderCapabilities Capabilities { get; } = CreateCapabilities();

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            IReadOnlyList<string> containers = await dbContext.StorageBlobs.AsNoTracking()
                .Select(blob => blob.Container)
                .Distinct()
                .OrderBy(container => container)
                .ToArrayAsync(cancellationToken);
            return Result<IReadOnlyList<string>>.Success(containers);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<string>>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    /// <summary>
    /// Creates the provider capabilities for the Entity Framework blob-store provider.
    /// </summary>
    /// <returns>The provider capability descriptor.</returns>
    /// <example>
    /// <code>
    /// var capabilities = EntityFrameworkBlobStoreProvider&lt;AppDbContext&gt;.CreateCapabilities();
    /// </code>
    /// </example>
    public static BlobStoreProviderCapabilities CreateCapabilities() => new()
    {
        SupportsContinuationPaging = true,
        SupportsPrefixListing = true,
        SupportsFullContainerScan = true,
        SupportsProperties = true,
        SupportsContentType = true,
        SupportsETag = true,
        SupportsContentHash = true,
        SupportsInternalLeases = true,
        SupportsConditionalPropertiesUpdate = true,
        SupportsStreamingUpload = true,
        SupportsStreamingDownload = true,
        SupportsExpiration = true,
        SupportsRetentionSweep = true,
        SupportsNativeRetention = true
    };

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(upload, this.options);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        await using var lease = this.CreateContextLease();
        var dbContext = lease.Context;
        BlobSnapshot snapshot = null;
        IDbContextTransaction transaction = null;

        try
        {
            transaction = await this.BeginTransactionIfSupportedAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var key = CreateKeyIdentity(upload.Key);
            snapshot = transaction is null
                ? await this.CreateSnapshotAsync(dbContext, key, cancellationToken).ConfigureAwait(false)
                : null;
            var blob = await this.QueryExactBlob(dbContext, key).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (blob is not null && upload.OverwriteMode == BlobOverwriteMode.FailIfExists)
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<BlobInfo>.Failure(new BlobStoreConflictError(
                    $"Blob with container '{upload.Key.Container}' and name '{upload.Key.Name}' already exists."));
            }

            var now = DateTimeOffset.UtcNow;
            var createdAt = blob?.CreatedAt ?? now;
            if (blob is null)
            {
                blob = new StorageBlob
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Container = upload.Key.Container,
                    Name = upload.Key.Name,
                    ContainerHash = key.ContainerHash,
                    NameHash = key.NameHash,
                    CreatedAt = createdAt,
                    LastModifiedAt = now,
                    Length = 0,
                    ETag = CreateETag()
                };
                this.AcquireNewLease(blob);
                dbContext.StorageBlobs.Add(blob);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (!await this.TryAcquireLeaseAsync(dbContext, blob, cancellationToken).ConfigureAwait(false))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<BlobInfo>.Failure(new BlobStoreLeaseError(
                    $"Could not acquire blob upload lease for '{upload.Key.Container}/{upload.Key.Name}'."));
            }

            await this.DeleteChunksAsync(dbContext, blob.Id, cancellationToken).ConfigureAwait(false);

            var contentHash = await this.WriteChunksAsync(
                dbContext,
                blob.Id,
                upload.Content,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(upload.ExpectedContentHash) &&
                !string.Equals(upload.ExpectedContentHash, contentHash.Hash, StringComparison.Ordinal))
            {
                throw new BlobStoreResultException(new BlobStoreIntegrityError(
                    "ExpectedContentHash does not match uploaded content."));
            }

            blob.Length = contentHash.Length;
            blob.ContentHash = contentHash.Hash;
            blob.ContentTypeMimeType = upload.ContentType?.MimeType();
            blob.ETag = CreateETag();
            blob.CreatedAt = createdAt;
            blob.LastModifiedAt = DateTimeOffset.UtcNow;
            blob.ExpiresAt = ToUtc(upload.ExpiresAt);
            blob.Properties = ToDictionary(upload.Properties);
            blob.LeaseId = null;
            blob.LeaseAcquiredBy = null;
            blob.LeaseAcquiredUntil = null;
            blob.ConcurrencyVersion = Guid.NewGuid();

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result<BlobInfo>.Success(ToInfo(blob));
        }
        catch (BlobStoreResultException exception)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<BlobInfo>.Failure(exception.Error);
        }
        catch (OperationCanceledException)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<BlobInfo>.Failure(new BlobStoreConflictError(
                $"Blob with container '{upload.Key.Container}' and name '{upload.Key.Name}' already exists."));
        }
        catch (Exception exception)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<BlobInfo>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<BlobDownload>.Failure(validation);
        }

        var lease = this.CreateContextLease();
        try
        {
            var identity = CreateKeyIdentity(key);
            var blob = await this.QueryExactBlob(lease.Context, identity)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (blob is null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
                return Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key));
            }

            return Result<BlobDownload>.Success(new BlobDownload
            {
                Info = ToInfo(blob),
                Content = new BlobChunkReadStream(lease, blob.Id, blob.Length)
            });
        }
        catch (OperationCanceledException)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            return Result<BlobDownload>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        await using var lease = this.CreateContextLease();
        try
        {
            var identity = CreateKeyIdentity(key);
            var blob = await this.QueryExactBlob(lease.Context, identity)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return blob is null
                ? Result<BlobInfo>.Failure(new BlobStoreNotFoundError(key))
                : Result<BlobInfo>.Success(ToInfo(blob));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobInfo>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UpdatePropertiesAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(update);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        await using var lease = this.CreateContextLease();
        var dbContext = lease.Context;
        StorageBlob snapshot = null;
        IDbContextTransaction transaction = null;

        try
        {
            transaction = await this.BeginTransactionIfSupportedAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var identity = CreateKeyIdentity(update.Key);
            var blob = await this.QueryExactBlob(dbContext, identity).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (blob is null)
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<BlobInfo>.Failure(new BlobStoreNotFoundError(update.Key));
            }

            snapshot = CloneBlob(blob);

            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) &&
                !string.Equals(update.IfMatchETag, blob.ETag, StringComparison.Ordinal))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<BlobInfo>.Failure(new BlobStoreConflictError(
                    $"Blob ETag does not match for '{update.Key.Container}/{update.Key.Name}'."));
            }

            if (!await this.TryAcquireLeaseAsync(dbContext, blob, cancellationToken).ConfigureAwait(false))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result<BlobInfo>.Failure(new BlobStoreLeaseError(
                    $"Could not acquire blob property update lease for '{update.Key.Container}/{update.Key.Name}'."));
            }

            blob.ContentTypeMimeType = update.ContentType?.MimeType();
            blob.ExpiresAt = ToUtc(update.ExpiresAt);
            blob.ETag = CreateETag();
            blob.LastModifiedAt = DateTimeOffset.UtcNow;
            blob.Properties = ToDictionary(update.Properties);
            blob.LeaseId = null;
            blob.LeaseAcquiredBy = null;
            blob.LeaseAcquiredUntil = null;
            blob.ConcurrencyVersion = Guid.NewGuid();

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result<BlobInfo>.Success(ToInfo(blob));
        }
        catch (OperationCanceledException)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreMetadataSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreMetadataSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            return Result<BlobInfo>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result<bool>.Failure((IResult)validation);
        }

        await using var lease = this.CreateContextLease();
        try
        {
            var identity = CreateKeyIdentity(key);
            var exists = await this.QueryExactBlob(lease.Context, identity)
                .AsNoTracking()
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(exists);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<bool>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobQueryValidator.NormalizeAndValidate(
            ProviderName,
            query,
            this.options,
            this.Capabilities,
            this.continuationTokenProtector);
        if (validation.IsFailure)
        {
            return Result<BlobPage>.Failure(validation);
        }

        await using var lease = this.CreateContextLease();
        try
        {
            var rows = lease.Context.StorageBlobs
                .AsNoTracking()
                .Where(e => e.Container == validation.Value.Query.Container);

            if (!string.IsNullOrEmpty(validation.Value.Query.Prefix))
            {
                rows = rows.Where(e => e.Name.StartsWith(validation.Value.Query.Prefix));
            }

            if (!string.IsNullOrWhiteSpace(validation.Value.ContinuationToken?.Name))
            {
                rows = rows.Where(e => string.Compare(e.Name, validation.Value.ContinuationToken.Name) > 0);
            }

            var pageRows = await rows
                .OrderBy(e => e.Name)
                .Select(e => new BlobInfoProjection(
                    e.Container,
                    e.Name,
                    e.Length,
                    e.ContentTypeMimeType,
                    e.ContentHash,
                    e.ETag,
                    e.CreatedAt,
                    e.LastModifiedAt,
                    e.ExpiresAt,
                    e.PropertiesJson))
                .Take(validation.Value.Take + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var items = pageRows
                .Take(validation.Value.Take)
                .Select(ToInfo)
                .ToList();
            var continuationToken = pageRows.Count > validation.Value.Take
                ? CreateContinuationToken(validation.Value.QueryHash, items[^1].Key)
                : null;

            return Result<BlobPage>.Success(new BlobPage
            {
                Items = items,
                ContinuationToken = continuationToken
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobPage>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return validation;
        }

        await using var lease = this.CreateContextLease();
        var dbContext = lease.Context;
        BlobSnapshot snapshot = null;
        IDbContextTransaction transaction = null;

        try
        {
            transaction = await this.BeginTransactionIfSupportedAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var identity = CreateKeyIdentity(key);
            snapshot = dbContext.Database.IsRelational()
                ? null
                : await this.CreateSnapshotAsync(dbContext, identity, cancellationToken).ConfigureAwait(false);
            var blob = await this.QueryExactBlob(dbContext, identity).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (blob is null)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return Result.Success();
            }

            if (!string.IsNullOrWhiteSpace(options?.IfMatchETag) &&
                !string.Equals(options.IfMatchETag, blob.ETag, StringComparison.Ordinal))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result.Failure(new BlobStoreConflictError(
                    $"Blob ETag does not match for '{key.Container}/{key.Name}'."));
            }

            if (!await this.TryAcquireLeaseAsync(dbContext, blob, cancellationToken).ConfigureAwait(false))
            {
                await this.RollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result.Failure(new BlobStoreLeaseError(
                    $"Could not acquire blob delete lease for '{key.Container}/{key.Name}'."));
            }

            await this.DeleteChunksAsync(dbContext, blob.Id, cancellationToken).ConfigureAwait(false);
            dbContext.StorageBlobs.Remove(blob);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            await this.RestoreSnapshotIfNeededAsync(dbContext, snapshot, transaction, CancellationToken.None).ConfigureAwait(false);
            return Result.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<BlobRetentionSweepResult>> SweepExpiredAsync(
        BlobRetentionSweepRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await using var lease = this.CreateContextLease();
        var dbContext = lease.Context;
        var deleted = 0;
        var deletedKeys = new List<BlobKey>();
        var skipped = 0;
        var batches = 0;

        try
        {
            for (var batch = 0; batch < Math.Max(1, request.MaxBatches); batch++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidates = await dbContext.StorageBlobs
                    .Where(e => e.ExpiresAt != null && e.ExpiresAt <= request.ExpiresOnOrBefore)
                    .OrderBy(e => e.ExpiresAt)
                    .ThenBy(e => e.Id)
                    .Take(Math.Max(1, request.BatchSize))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (candidates.Count == 0)
                {
                    break;
                }

                batches++;
                IDbContextTransaction transaction = null;
                try
                {
                    transaction = await this.BeginTransactionIfSupportedAsync(dbContext, cancellationToken).ConfigureAwait(false);

                    foreach (var blob in candidates)
                    {
                        if (!await this.TryAcquireLeaseAsync(dbContext, blob, cancellationToken).ConfigureAwait(false))
                        {
                            skipped++;
                            continue;
                        }

                        await this.DeleteChunksAsync(dbContext, blob.Id, cancellationToken).ConfigureAwait(false);
                        dbContext.StorageBlobs.Remove(blob);
                        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        deleted++;
                        deletedKeys.Add(new(blob.Container, blob.Name));
                    }

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    await this.RollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync().ConfigureAwait(false);
                    }
                }

                if (request.BatchDelay > TimeSpan.Zero)
                {
                    await Task.Delay(request.BatchDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return Result<BlobRetentionSweepResult>.Success(new BlobRetentionSweepResult
            {
                StoreName = request.StoreName,
                ProviderName = request.ProviderName,
                BatchCount = batches,
                DeletedCount = deleted,
                DeletedKeys = deletedKeys,
                SkippedCount = skipped,
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result<BlobRetentionSweepResult>.Failure(new BlobStoreProviderError(exception.GetFullMessage()));
        }
    }

    private ContextLease CreateContextLease()
    {
        var scope = this.scopeFactory.CreateScope();
        return new ContextLease(scope, scope.ServiceProvider.GetRequiredService<TContext>());
    }

    private async Task<IDbContextTransaction> BeginTransactionIfSupportedAsync(
        TContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RollbackAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RestoreSnapshotIfNeededAsync(
        TContext dbContext,
        BlobSnapshot snapshot,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null || snapshot is null || dbContext.Database.IsRelational())
        {
            return;
        }

        var rows = await dbContext.StorageBlobs
            .Where(e => e.ContainerHash == snapshot.Key.ContainerHash && e.NameHash == snapshot.Key.NameHash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count != 0)
        {
            dbContext.StorageBlobs.RemoveRange(rows);
        }

        var orphanChunks = await dbContext.StorageBlobChunks
            .Where(e => rows.Select(row => row.Id).Contains(e.BlobId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (orphanChunks.Count != 0)
        {
            dbContext.StorageBlobChunks.RemoveRange(orphanChunks);
        }

        if (snapshot.Blob is not null)
        {
            dbContext.StorageBlobs.Add(snapshot.Blob);
            dbContext.StorageBlobChunks.AddRange(snapshot.Chunks);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RestoreMetadataSnapshotIfNeededAsync(
        TContext dbContext,
        StorageBlob snapshot,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null || snapshot is null || dbContext.Database.IsRelational())
        {
            return;
        }

        var blob = await dbContext.StorageBlobs
            .SingleOrDefaultAsync(e => e.Id == snapshot.Id, cancellationToken)
            .ConfigureAwait(false);
        if (blob is null)
        {
            dbContext.StorageBlobs.Add(snapshot);
        }
        else
        {
            blob.Container = snapshot.Container;
            blob.Name = snapshot.Name;
            blob.ContainerHash = snapshot.ContainerHash;
            blob.NameHash = snapshot.NameHash;
            blob.Length = snapshot.Length;
            blob.ContentTypeMimeType = snapshot.ContentTypeMimeType;
            blob.ContentHash = snapshot.ContentHash;
            blob.ETag = snapshot.ETag;
            blob.CreatedAt = snapshot.CreatedAt;
            blob.LastModifiedAt = snapshot.LastModifiedAt;
            blob.ExpiresAt = ToUtc(snapshot.ExpiresAt);
            blob.Properties = new Dictionary<string, object>(snapshot.Properties, StringComparer.OrdinalIgnoreCase);
            blob.LeaseId = snapshot.LeaseId;
            blob.LeaseAcquiredBy = snapshot.LeaseAcquiredBy;
            blob.LeaseAcquiredUntil = snapshot.LeaseAcquiredUntil;
            blob.ConcurrencyVersion = snapshot.ConcurrencyVersion;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<BlobSnapshot> CreateSnapshotAsync(
        TContext dbContext,
        KeyIdentity key,
        CancellationToken cancellationToken)
    {
        var blob = await dbContext.StorageBlobs
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.ContainerHash == key.ContainerHash && e.NameHash == key.NameHash, cancellationToken)
            .ConfigureAwait(false);

        if (blob is null)
        {
            return new BlobSnapshot(key, null, []);
        }

        var chunks = await dbContext.StorageBlobChunks
            .AsNoTracking()
            .Where(e => e.BlobId == blob.Id)
            .OrderBy(e => e.Index)
            .Select(e => new StorageBlobChunk
            {
                BlobId = e.BlobId,
                Index = e.Index,
                Content = e.Content,
                Length = e.Length
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BlobSnapshot(key, CloneBlob(blob), chunks);
    }

    private async Task DeleteChunksAsync(TContext dbContext, string blobId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational() && !IsSqlite(dbContext))
        {
            await dbContext.StorageBlobChunks
                .Where(e => e.BlobId == blobId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var chunks = await dbContext.StorageBlobChunks
            .Where(e => e.BlobId == blobId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.StorageBlobChunks.RemoveRange(chunks);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContentWriteResult> WriteChunksAsync(
        TContext dbContext,
        string blobId,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bufferSize = Math.Max(1, this.options.ChunkSize);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        var total = 0L;
        var index = 0;
        var pendingChunkBytes = 0L;
        var pendingChunks = new List<StorageBlobChunk>(
            Math.Min(this.options.ChunkFlushCount, 128));
        var flushCount = 0;

        try
        {
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (this.options.MaxBlobSize is not null && total > this.options.MaxBlobSize.Value)
                {
                    throw new BlobStoreResultException(new BlobStoreSizeLimitExceededError(total, this.options.MaxBlobSize.Value));
                }

                hash.AppendData(buffer.AsSpan(0, read));
                var bytes = new byte[read];
                buffer.AsSpan(0, read).CopyTo(bytes);
                var chunk = new StorageBlobChunk
                {
                    BlobId = blobId,
                    Index = index++,
                    Content = bytes,
                    Length = read
                };

                dbContext.StorageBlobChunks.Add(chunk);
                pendingChunks.Add(chunk);
                pendingChunkBytes += read;

                if (pendingChunks.Count >= this.options.ChunkFlushCount ||
                    pendingChunkBytes >= this.options.MaxPendingChunkBytes)
                {
                    await this.FlushChunksAsync(
                        dbContext,
                        pendingChunks,
                        pendingChunkBytes,
                        cancellationToken).ConfigureAwait(false);
                    pendingChunkBytes = 0;
                    flushCount++;
                }
            }

            if (pendingChunks.Count > 0)
            {
                await this.FlushChunksAsync(
                    dbContext,
                    pendingChunks,
                    pendingChunkBytes,
                    cancellationToken).ConfigureAwait(false);
                flushCount++;
            }

            return new ContentWriteResult(
                $"{BlobContentHash.Prefix}{Convert.ToHexStringLower(hash.GetHashAndReset())}",
                total,
                index,
                flushCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task FlushChunksAsync(
        TContext dbContext,
        List<StorageBlobChunk> pendingChunks,
        long pendingChunkBytes,
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var chunkCount = pendingChunks.Count;

        foreach (var chunk in pendingChunks)
        {
            dbContext.Entry(chunk).State = EntityState.Detached;
        }

        pendingChunks.Clear();
        this.RecordChunkFlush(chunkCount, pendingChunkBytes);
    }

    private void RecordChunkFlush(int chunkCount, long byteCount)
    {
        var tags = new KeyValuePair<string, object>[]
        {
            new("provider", ProviderName),
            new("store", this.storeName)
        };

        try
        {
            this.chunksWritten?.Add(chunkCount, tags);
            this.chunkFlushes?.Add(1, tags);
            this.chunksPerFlush?.Record(chunkCount, tags);
            this.bytesPerFlush?.Record(byteCount, tags);
            TypedLogger.LogChunkFlush(
                this.logger,
                this.storeName,
                chunkCount,
                byteCount);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and
            not StackOverflowException and
            not AccessViolationException)
        {
            // Flush telemetry is best effort and must not change a successful persistence outcome.
        }
    }

    private async Task<bool> TryAcquireLeaseAsync(TContext dbContext, StorageBlob blob, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var owner = this.GetLeaseOwner();
        var leaseUntil = now.Add(this.options.LeaseDuration);

        if (dbContext.Database.IsRelational() && !IsSqlite(dbContext))
        {
            var currentVersion = blob.ConcurrencyVersion;
            var leaseId = Guid.NewGuid().ToString("N");
            var nextVersion = Guid.NewGuid();
            var affected = await dbContext.StorageBlobs
                .Where(e => e.Id == blob.Id &&
                    e.ConcurrencyVersion == currentVersion &&
                    (e.LeaseAcquiredUntil == null || e.LeaseAcquiredUntil < now || e.LeaseAcquiredBy == owner))
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.LeaseId, _ => leaseId)
                        .SetProperty(e => e.LeaseAcquiredBy, _ => owner)
                        .SetProperty(e => e.LeaseAcquiredUntil, _ => leaseUntil)
                        .SetProperty(e => e.ConcurrencyVersion, _ => nextVersion),
                    cancellationToken)
                .ConfigureAwait(false);

            if (affected == 0)
            {
                return false;
            }

            await dbContext.Entry(blob).ReloadAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (blob.LeaseAcquiredUntil is not null &&
            blob.LeaseAcquiredUntil >= now &&
            !string.Equals(blob.LeaseAcquiredBy, owner, StringComparison.Ordinal))
        {
            return false;
        }

        blob.LeaseId = Guid.NewGuid().ToString("N");
        blob.LeaseAcquiredBy = owner;
        blob.LeaseAcquiredUntil = leaseUntil;
        blob.ConcurrencyVersion = Guid.NewGuid();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private void AcquireNewLease(StorageBlob blob)
    {
        blob.LeaseId = Guid.NewGuid().ToString("N");
        blob.LeaseAcquiredBy = this.GetLeaseOwner();
        blob.LeaseAcquiredUntil = DateTimeOffset.UtcNow.Add(this.options.LeaseDuration);
        blob.ConcurrencyVersion = Guid.NewGuid();
    }

    private string GetLeaseOwner() => string.IsNullOrWhiteSpace(this.options.LeaseOwner)
        ? this.leaseOwner
        : this.options.LeaseOwner;

    private IQueryable<StorageBlob> QueryExactBlob(TContext dbContext, KeyIdentity key) =>
        dbContext.StorageBlobs.Where(e => e.ContainerHash == key.ContainerHash && e.NameHash == key.NameHash);

    private static bool IsSqlite(DbContext dbContext) =>
        dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private static KeyIdentity CreateKeyIdentity(BlobKey key) =>
        new(key.Container, HashHelper.ComputeSha256(key.Container), key.Name, HashHelper.ComputeSha256(key.Name));

    private static string CreateETag() => $"\"{Guid.NewGuid():N}\"";

    private static DateTimeOffset? ToUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static IDictionary<string, object> ToDictionary(PropertyBag properties) =>
        properties?.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase) ??
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    private static PropertyBag ToPropertyBag(IDictionary<string, object> properties) =>
        properties is null ? new PropertyBag() : new PropertyBag(properties);

    private static PropertyBag ToPropertyBag(string propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson))
        {
            return new PropertyBag();
        }

        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(
            propertiesJson,
            DefaultJsonSerializerOptions.Create());

        return ToPropertyBag(properties);
    }

    private static BlobInfo ToInfo(StorageBlob blob) => new()
    {
        Key = new BlobKey(blob.Container, blob.Name),
        Length = blob.Length,
        ContentType = string.IsNullOrWhiteSpace(blob.ContentTypeMimeType)
            ? null
            : ContentTypeExtensions.FromMimeType(blob.ContentTypeMimeType),
        ContentHash = blob.ContentHash,
        ETag = blob.ETag,
        CreatedAt = blob.CreatedAt,
        LastModifiedAt = blob.LastModifiedAt,
        ExpiresAt = ToUtc(blob.ExpiresAt),
        Properties = ToPropertyBag(blob.Properties)
    };

    private static BlobInfo ToInfo(BlobInfoProjection blob) => new()
    {
        Key = new BlobKey(blob.Container, blob.Name),
        Length = blob.Length,
        ContentType = string.IsNullOrWhiteSpace(blob.ContentTypeMimeType)
            ? null
            : ContentTypeExtensions.FromMimeType(blob.ContentTypeMimeType),
        ContentHash = blob.ContentHash,
        ETag = blob.ETag,
        CreatedAt = blob.CreatedAt,
        LastModifiedAt = blob.LastModifiedAt,
        ExpiresAt = ToUtc(blob.ExpiresAt),
        Properties = ToPropertyBag(blob.PropertiesJson)
    };

    private string CreateContinuationToken(string queryHash, BlobKey lastKey)
    {
        var result = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
        {
            Provider = ProviderName,
            QueryHash = queryHash,
            Container = lastKey.Container,
            Name = lastKey.Name
        }, this.continuationTokenProtector);

        return result.IsSuccess ? result.Value : null;
    }

    private static StorageBlob CloneBlob(StorageBlob blob) => new()
    {
        Id = blob.Id,
        Container = blob.Container,
        Name = blob.Name,
        ContainerHash = blob.ContainerHash,
        NameHash = blob.NameHash,
        Length = blob.Length,
        ContentTypeMimeType = blob.ContentTypeMimeType,
        ContentHash = blob.ContentHash,
        ETag = blob.ETag,
        CreatedAt = blob.CreatedAt,
        LastModifiedAt = blob.LastModifiedAt,
        ExpiresAt = ToUtc(blob.ExpiresAt),
        Properties = new Dictionary<string, object>(blob.Properties, StringComparer.OrdinalIgnoreCase),
        LeaseId = blob.LeaseId,
        LeaseAcquiredBy = blob.LeaseAcquiredBy,
        LeaseAcquiredUntil = blob.LeaseAcquiredUntil,
        ConcurrencyVersion = blob.ConcurrencyVersion
    };

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetFullMessage().Contains("unique", StringComparison.OrdinalIgnoreCase) ||
        exception.GetFullMessage().Contains("duplicate", StringComparison.OrdinalIgnoreCase);

    private readonly record struct KeyIdentity(string Container, string ContainerHash, string Name, string NameHash);

    private sealed record BlobInfoProjection(
        string Container,
        string Name,
        long Length,
        string ContentTypeMimeType,
        string ContentHash,
        string ETag,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastModifiedAt,
        DateTimeOffset? ExpiresAt,
        string PropertiesJson);

    private sealed record BlobSnapshot(KeyIdentity Key, StorageBlob Blob, IReadOnlyList<StorageBlobChunk> Chunks);

    private readonly record struct ContentWriteResult(
        string Hash,
        long Length,
        int ChunkCount,
        int FlushCount);

    private sealed class BlobStoreResultException(IResultError error) : Exception(error.Message)
    {
        public IResultError Error { get; } = error;
    }

    private static partial class TypedLogger
    {
        [LoggerMessage(
            0,
            LogLevel.Debug,
            "blob EF chunk group flushed (store={StoreName}, provider=Entity Framework, chunks={ChunkCount}, bytes={ByteCount})")]
        public static partial void LogChunkFlush(
            ILogger logger,
            string storeName,
            int chunkCount,
            long byteCount);
    }

    private sealed class BlobChunkReadStream : Stream
    {
        private readonly ContextLease lease;
        private readonly string blobId;
        private byte[] currentChunk;
        private int currentOffset;
        private int nextIndex;
        private bool completed;
        private bool disposed;

        public BlobChunkReadStream(ContextLease lease, string blobId, long length)
        {
            this.lease = lease;
            this.blobId = blobId;
            this.Length = length;
        }

        public override bool CanRead => !this.disposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position { get; set; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            this.ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            if (buffer.Length == 0)
            {
                return 0;
            }

            if (!await this.EnsureChunkAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            var available = this.currentChunk.Length - this.currentOffset;
            var count = Math.Min(buffer.Length, available);
            this.currentChunk.AsMemory(this.currentOffset, count).CopyTo(buffer);
            this.currentOffset += count;
            this.Position += count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException("Blob downloads do not support seeking.");

        public override void SetLength(long value) =>
            throw new NotSupportedException("Blob downloads do not support setting length.");

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Blob downloads are read-only.");

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.lease.Dispose();
                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!this.disposed)
            {
                await this.lease.DisposeAsync().ConfigureAwait(false);
                this.disposed = true;
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<bool> EnsureChunkAsync(CancellationToken cancellationToken)
        {
            if (this.completed)
            {
                return false;
            }

            if (this.currentChunk is not null && this.currentOffset < this.currentChunk.Length)
            {
                return true;
            }

            var chunk = await this.lease.Context.StorageBlobChunks
                .AsNoTracking()
                .Where(e => e.BlobId == this.blobId && e.Index == this.nextIndex)
                .Select(e => new { e.Content, e.Length })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (chunk is null)
            {
                this.completed = true;
                return false;
            }

            this.nextIndex++;
            this.currentOffset = 0;
            this.currentChunk = chunk.Length == chunk.Content.Length
                ? chunk.Content
                : chunk.Content.Take(chunk.Length).ToArray();

            return true;
        }
    }

    private sealed class ContextLease : IDisposable, IAsyncDisposable
    {
        private readonly IServiceScope scope;
        public ContextLease(IServiceScope scope, TContext context)
        {
            this.scope = scope;
            this.Context = context;
        }

        public TContext Context { get; }

        public void Dispose()
        {
            this.scope.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (this.scope is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            this.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
