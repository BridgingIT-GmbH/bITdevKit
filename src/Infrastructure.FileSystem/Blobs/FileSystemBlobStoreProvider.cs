namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Stores blobs as immutable files with replaceable metadata and per-key local process coordination.</summary>
/// <example><code>var provider = new FileSystemBlobStoreProvider(options);</code></example>
public sealed class FileSystemBlobStoreProvider : IBlobStoreProvider, IBlobStoreContainerCatalog, IBlobStoreRetentionProvider
{
    private readonly FileSystemStore store;
    private readonly BlobStoreOptions options;
    private readonly IContinuationTokenProtector protector;

    /// <summary>Gets the low-level store for trusted diagnostics, ownership-aware tooling and explicit quiescent recovery.</summary>
    /// <remarks>Ordinary blob operations should use the provider or client methods. Low-level mutations require the store's ownership and persistence protocol.</remarks>
    /// <example><code>var configuration = provider.Store.Options;</code></example>
    public FileSystemStore Store => this.store;

    /// <summary>Creates a filesystem blob provider. Unsupported persistence modes fail on access.</summary>
    /// <example><code>var provider = new FileSystemBlobStoreProvider(options, new BlobStoreOptions());</code></example>
    public FileSystemBlobStoreProvider(FileSystemStorageOptions options, BlobStoreOptions blobOptions = null, IContinuationTokenProtector protector = null)
    {
        this.store = new(options);
        this.options = blobOptions ?? new();
        this.protector = protector;
    }

    /// <inheritdoc />
    public BlobStoreProviderCapabilities Capabilities { get; } = new()
    {
        SupportsContinuationPaging = true,
        SupportsPrefixListing = true,
        SupportsFullContainerScan = true,
        SupportsProperties = true,
        SupportsContentType = true,
        SupportsETag = true,
        SupportsContentHash = true,
        SupportsConditionalPropertiesUpdate = true,
        SupportsStreamingUpload = true,
        SupportsStreamingDownload = true,
        SupportsExpiration = true,
        SupportsRetentionSweep = true
    };

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync(async () =>
        {
            var validation = BlobValidator.Validate(upload, this.options);

            if (validation.IsFailure)
            {
                return Result<BlobInfo>.Failure(validation);
            }

            var identity = Identity(upload.Key);
            var path = this.store.Resolve(identity);

            using var staged = await this.store.StageAsync(
                path,
                upload.Content,
                this.options.MaxBlobSize,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(upload.ExpectedContentHash) && upload.ExpectedContentHash != staged.Hash)
            {
                return Result<BlobInfo>.Failure(new BlobStoreIntegrityError("Uploaded content hash differs."));
            }

            using var authority = await this.store.AcquireAsync(
                path,
                immediate: false,
                cancellationToken).ConfigureAwait(false);
            var current = this.store.Read(path, identity);

            if (current is not null && upload.OverwriteMode == BlobOverwriteMode.FailIfExists)
            {
                return Result<BlobInfo>.Failure(new BlobStoreConflictError("A physical blob already exists."));
            }

            var now = DateTimeOffset.UtcNow;
            var info = new BlobInfo
            {
                Key = upload.Key,
                Length = staged.Length,
                ContentHash = staged.Hash,
                ContentType = upload.ContentType,
                ETag = Guid.NewGuid().ToString("N"),
                CreatedAt = current?.Blob.CreatedAt ?? now,
                LastModifiedAt = now,
                ExpiresAt = upload.ExpiresAt?.ToUniversalTime(),
                Properties = upload.Properties?.Clone() ?? new()
            };

            var record = new FileSystemRecord
            {
                Identity = identity,
                Payload = staged.File,
                Length = staged.Length,
                Blob = info
            };

            this.store.Publish(path, record, authority, cancellationToken, staged);

            return Result<BlobInfo>.Success(info);
        });
    }

    /// <inheritdoc />
    public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync(async () =>
        {
            var validation = BlobValidator.Validate(key);

            if (validation.IsFailure)
            {
                return Result<BlobDownload>.Failure(validation);
            }

            var path = this.store.Resolve(Identity(key), false);
            using var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            var record = this.store.ReadUnderAuthority(path, authority, Identity(key));

            if (record is null)
            {
                return Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key));
            }

            var download = new BlobDownload
            {
                Content = this.store.OpenPayload(path, record),
                Info = record.Blob
            };

            return Result<BlobDownload>.Success(download);
        });
    }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = BlobValidator.Validate(key);

            if (validation.IsFailure)
            {
                return Result<BlobInfo>.Failure(validation);
            }

            var path = this.store.Resolve(Identity(key), false);
            var record = this.store.Read(path, Identity(key));
            await Task.CompletedTask;

            return record is null ? Result<BlobInfo>.Failure(new BlobStoreNotFoundError(key)) : Result<BlobInfo>.Success(record.Blob);
        });
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        var result = await this.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Result<bool>.Success(true) : result.HasError<BlobStoreNotFoundError>() ? Result<bool>.Success(false) : Result<bool>.Failure((IResult)result);
    }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync(async () =>
        {
            var validation = BlobValidator.Validate(update);

            if (validation.IsFailure)
            {
                return Result<BlobInfo>.Failure(validation);
            }

            var identity = Identity(update.Key);
            var path = this.store.Resolve(identity, false);
            using var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            var current = this.store.ReadUnderAuthority(path, authority, identity);

            if (current is null)
            {
                return Result<BlobInfo>.Failure(new BlobStoreNotFoundError(update.Key));
            }

            if (!string.IsNullOrWhiteSpace(update.IfMatchETag) && update.IfMatchETag != current.Blob.ETag)
            {
                return Result<BlobInfo>.Failure(new BlobStoreConflictError("Blob ETag differs."));
            }

            var info = new BlobInfo
            {
                Key = current.Blob.Key,
                Length = current.Length,
                ContentHash = current.Blob.ContentHash,
                ContentType = update.ContentType,
                ETag = Guid.NewGuid().ToString("N"),
                CreatedAt = current.Blob.CreatedAt,
                LastModifiedAt = DateTimeOffset.UtcNow,
                ExpiresAt = update.ExpiresAt?.ToUniversalTime(),
                Properties = update.Properties?.Clone() ?? new()
            };
            this.store.Publish(path, current with { Blob = info }, authority, cancellationToken);

            return Result<BlobInfo>.Success(info);
        });
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(BlobKey key, BlobDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await FileSystemResults.BlobAsync(async () =>
        {
            var validation = BlobValidator.Validate(key);

            if (validation.IsFailure)
            {
                return Result<bool>.Failure((IResult)validation);
            }

            var identity = Identity(key);
            var path = this.store.Resolve(identity, false);
            using var authority = await this.store.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            var record = this.store.ReadUnderAuthority(path, authority, identity);

            if (record is not null && !string.IsNullOrWhiteSpace(options?.IfMatchETag) && record.Blob.ETag != options.IfMatchETag)
            {
                return Result<bool>.Failure(new BlobStoreConflictError("Blob ETag differs."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.store.Delete(path, record, authority, cancellationToken);

            return Result<bool>.Success(true);
        }).ConfigureAwait(false);

        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    /// <inheritdoc />
    public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync(async () =>
        {
            var validation = BlobQueryValidator.NormalizeAndValidate("FileSystem", query, this.options, this.Capabilities, this.protector);

            if (validation.IsFailure)
            {
                return Result<BlobPage>.Failure(validation);
            }

            var normalized = validation.Value;
            var rows = new SortedDictionary<string, BlobInfo>(StringComparer.Ordinal);
            var inspected = 0;

            foreach (var path in this.store.Paths("blobs", normalized.Query.Container))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++inspected > this.store.MaxQueryMetadataEntries)
                {
                    return Result<BlobPage>.Failure(new BlobStoreQueryTooBroadError("Metadata scan bound exceeded."));
                }

                var record = this.store.Read(path);

                if (record?.Blob is not { } info || info.Key.Container != normalized.Query.Container
                || !info.Key.Name.StartsWith(normalized.Query.Prefix ?? "", StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(normalized.ContinuationToken?.Name) && string.CompareOrdinal(info.Key.Name, normalized.ContinuationToken.Name) <= 0))
                {
                    continue;
                }

                rows[info.Key.Name] = info;

                if (rows.Count > (long)normalized.Take + 1)
                {
                    rows.Remove(rows.Keys.Last());
                }
            }

            var items = rows.Values.Take(normalized.Take).ToArray();
            string token = null;

            if (rows.Count > normalized.Take)
            {
                var serialized = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken { Provider = "FileSystem", QueryHash = normalized.QueryHash,
                Container = normalized.Query.Container, Name = items[^1].Key.Name }, this.protector);

                if (serialized.IsFailure)
                {
                    return Result<BlobPage>.Failure(serialized);
                }

                token = serialized.Value;
            }

            await Task.CompletedTask;

            return Result<BlobPage>.Success(new() { Items = items, ContinuationToken = token });
        });
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync<IReadOnlyList<string>>(async () =>
        {
            var containers = new SortedSet<string>(StringComparer.Ordinal);
            var inspected = 0;

            foreach (var path in this.store.Paths("blobs"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++inspected > this.store.MaxQueryMetadataEntries)
                {
                    return Result<IReadOnlyList<string>>.Failure(new BlobStoreQueryTooBroadError("Metadata scan bound exceeded."));
                }

                var record = this.store.Read(path);

                if (record?.Blob is not null)
                {
                    containers.Add(record.Blob.Key.Container);
                }
            }

            await Task.CompletedTask;

            return Result<IReadOnlyList<string>>.Success(containers.ToArray());
        });
    }

    private static string[] Identity(BlobKey key)
    {
        return["blobs", key.Container, key.Name];
    }

    /// <inheritdoc />
    public Task<Result<BlobRetentionSweepResult>> SweepExpiredAsync(BlobRetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        return FileSystemResults.BlobAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.BatchSize <= 0 || request.MaxBatches <= 0 || request.BatchDelay < TimeSpan.Zero)
            {
                return Result<BlobRetentionSweepResult>.Failure(new BlobStoreValidationError("Invalid retention bounds."));
            }

            var candidates = await this.store.RetentionCandidatesAsync("blobs", null, cancellationToken).ConfigureAwait(false);
            this.store.Fault?.Invoke("retention-discovered");
            var deleted = new List<BlobKey>();
            var skipped = 0;
            var limit = (long)request.BatchSize * request.MaxBatches;

            foreach (var path in candidates.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (deleted.Count >= limit)
                {
                    break;
                }

                using (var authority = await this.store.AcquireAsync(path, true, cancellationToken).ConfigureAwait(false))
                {
                    if (authority is null)
                    {
                        skipped++;
                        continue;
                    }

                    var record = this.store.Read(path);
                    this.store.TryCollect(path, authority);

                    if (record?.Blob is not { ExpiresAt: { } expiry } blob || expiry > request.ExpiresOnOrBefore)
                    {
                        continue;
                    }

                    this.store.Delete(path, record, authority, cancellationToken);
                    deleted.Add(blob.Key);
                }

                if (deleted.Count % request.BatchSize == 0 && request.BatchDelay > TimeSpan.Zero && deleted.Count < limit)
                {
                    await Task.Delay(request.BatchDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return Result<BlobRetentionSweepResult>.Success(new() { StoreName = request.StoreName, ProviderName = request.ProviderName,
                DeletedKeys = deleted, DeletedCount = deleted.Count, SkippedCount = skipped,
                BatchCount = (int)((deleted.Count + (long)request.BatchSize - 1) / request.BatchSize), CompletedAt = DateTimeOffset.UtcNow });
        });
    }
}