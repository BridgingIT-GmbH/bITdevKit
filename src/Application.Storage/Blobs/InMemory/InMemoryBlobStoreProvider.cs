// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Implements <see cref="IBlobStoreProvider" /> using process-local in-memory storage.
/// </summary>
/// <remarks>
/// This provider is intended for tests and local development scenarios where content should not outlive the current
/// process.
/// </remarks>
/// <example>
/// <code>
/// var provider = new InMemoryBlobStoreProvider();
/// var client = new BlobStoreClient(InMemoryBlobStoreProvider.ProviderName, provider);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryBlobStoreProvider" /> class.
/// </remarks>
/// <param name="context">The optional shared in-memory context.</param>
/// <param name="options">The optional blob-store options used by provider-side enforcement.</param>
/// <param name="continuationTokenProtector">The optional continuation-token protector.</param>
/// <example>
/// <code>
/// var provider = new InMemoryBlobStoreProvider(new InMemoryBlobStoreContext(), new BlobStoreOptions());
/// </code>
/// </example>
public sealed class InMemoryBlobStoreProvider(
    InMemoryBlobStoreContext context = null,
    BlobStoreOptions options = null,
    IContinuationTokenProtector continuationTokenProtector = null) : IBlobStoreProvider, IBlobStoreRetentionProvider, IBlobStoreContainerCatalog
{
    /// <summary>
    /// Gets the provider discriminator used for diagnostics and continuation-token binding.
    /// </summary>
    /// <example>
    /// <code>
    /// var providerName = InMemoryBlobStoreProvider.ProviderName;
    /// </code>
    /// </example>
    public const string ProviderName = "InMemory";

    private readonly BlobStoreOptions options = options ?? new BlobStoreOptions();

    /// <summary>
    /// Gets the in-memory context backing this provider.
    /// </summary>
    /// <example>
    /// <code>
    /// var context = provider.Context;
    /// </code>
    /// </example>
    public InMemoryBlobStoreContext Context { get; } = context ?? new InMemoryBlobStoreContext();

    /// <inheritdoc />
    public BlobStoreProviderCapabilities Capabilities { get; } = CreateCapabilities();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> containers = this.Context.GetSnapshot()
            .Select(entry => entry.Info.Key.Container)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(Result<IReadOnlyList<string>>.Success(containers));
    }

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

        using var buffer = new MemoryStream();
        var copy = await BlobSizeLimit.CopyToAsync(
            upload.Content,
            buffer,
            this.options.MaxBlobSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (copy.IsFailure)
        {
            return Result<BlobInfo>.Failure(copy);
        }

        var content = buffer.ToArray();
        var contentHash = $"{BlobContentHash.Prefix}{HashHelper.ComputeSha256(content)}";
        if (!string.IsNullOrWhiteSpace(upload.ExpectedContentHash) &&
            !string.Equals(upload.ExpectedContentHash, contentHash, StringComparison.Ordinal))
        {
            return Result<BlobInfo>.Failure(new BlobStoreIntegrityError("ExpectedContentHash does not match uploaded content."));
        }

        var now = DateTimeOffset.UtcNow;
        var info = new BlobInfo
        {
            Key = upload.Key,
            Length = content.LongLength,
            ContentType = upload.ContentType,
            ContentHash = contentHash,
            ETag = CreateETag(),
            CreatedAt = now,
            LastModifiedAt = now,
            ExpiresAt = ToUtc(upload.ExpiresAt),
            Properties = CloneProperties(upload.Properties)
        };
        if (!this.Context.TryStore(
                new InMemoryBlobEntry { Content = content, Info = info },
                upload.OverwriteMode == BlobOverwriteMode.FailIfExists,
                out var stored))
        {
            return Result<BlobInfo>.Failure(new BlobStoreConflictError(
                $"Blob with container '{upload.Key.Container}' and name '{upload.Key.Name}' already exists."));
        }

        return Result<BlobInfo>.Success(stored.Info);
    }

    /// <inheritdoc />
    public Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<BlobDownload>.Failure(validation));
        }

        if (!this.Context.TryGet(key, out var entry))
        {
            return Task.FromResult(Result<BlobDownload>.Failure(new BlobStoreNotFoundError(key)));
        }

        return Task.FromResult(Result<BlobDownload>.Success(new BlobDownload
        {
            Content = new MemoryStream(entry.Content, writable: false),
            Info = entry.Info
        }));
    }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> GetPropertiesAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<BlobInfo>.Failure(validation));
        }

        return Task.FromResult(this.Context.TryGet(key, out var entry)
            ? Result<BlobInfo>.Success(entry.Info)
            : Result<BlobInfo>.Failure(new BlobStoreNotFoundError(key)));
    }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UpdatePropertiesAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(update);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<BlobInfo>.Failure(validation));
        }

        var updated = this.Context.TryUpdate(
            update.Key,
            update.IfMatchETag,
            entry => new InMemoryBlobEntry
            {
                Content = entry.Content,
                Info = new BlobInfo
                {
                    Key = entry.Info.Key,
                    Length = entry.Info.Length,
                    ContentType = update.ContentType,
                    ContentHash = entry.Info.ContentHash,
                    ETag = CreateETag(),
                    CreatedAt = entry.Info.CreatedAt,
                    LastModifiedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = ToUtc(update.ExpiresAt),
                    Properties = CloneProperties(update.Properties)
                }
            },
            out var stored,
            out var etagMismatch);
        if (!updated)
        {
            if (etagMismatch)
            {
                return Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreConflictError("Blob ETag does not match.")));
            }

            return Task.FromResult(Result<BlobInfo>.Failure(new BlobStoreNotFoundError(update.Key)));
        }

        return Task.FromResult(Result<BlobInfo>.Success(stored.Info));
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<bool>.Failure((IResult)validation));
        }

        return Task.FromResult(Result<bool>.Success(this.Context.Contains(key)));
    }

    /// <inheritdoc />
    public Task<Result<BlobPage>> ListPageAsync(
        BlobQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobQueryValidator.NormalizeAndValidate(
            ProviderName,
            query,
            this.options,
            this.Capabilities,
            continuationTokenProtector);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<BlobPage>.Failure(validation));
        }

        var rows = this.Context.GetSnapshot()
                .Where(entry => string.Equals(entry.Info.Key.Container, validation.Value.Query.Container, StringComparison.Ordinal))
                .Where(entry => string.IsNullOrEmpty(validation.Value.Query.Prefix) ||
                    entry.Info.Key.Name.StartsWith(validation.Value.Query.Prefix, StringComparison.Ordinal))
                .OrderBy(entry => entry.Info.Key.Name, StringComparer.Ordinal)
                .ToList();

            if (!string.IsNullOrWhiteSpace(validation.Value.ContinuationToken?.Name))
            {
                rows = rows
                    .Where(entry => string.Compare(entry.Info.Key.Name, validation.Value.ContinuationToken.Name, StringComparison.Ordinal) > 0)
                    .ToList();
            }

            var pageRows = rows.Take(validation.Value.Take + 1).ToList();
            var items = pageRows
                .Take(validation.Value.Take)
                .Select(entry => CloneInfo(entry.Info))
                .ToList();
            var continuationToken = pageRows.Count > validation.Value.Take
                ? this.CreateContinuationToken(validation.Value.QueryHash, items[^1].Key)
                : null;

        return Task.FromResult(Result<BlobPage>.Success(new BlobPage
        {
            Items = items,
            ContinuationToken = continuationToken
        }));
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Task.FromResult(validation);
        }

        this.Context.TryRemove(key, options?.IfMatchETag, out var etagMismatch);
        if (etagMismatch)
        {
            return Task.FromResult(Result.Failure(new BlobStoreConflictError("Blob ETag does not match.")));
        }

        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public async Task<Result<BlobRetentionSweepResult>> SweepExpiredAsync(
        BlobRetentionSweepRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var deleted = 0;
        var deletedKeys = new List<BlobKey>();
        var batches = 0;
        for (var batch = 0; batch < Math.Max(1, request.MaxBatches); batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentBatchDeleted = this.Context.RemoveExpired(
                request.ExpiresOnOrBefore,
                request.BatchSize);

            if (currentBatchDeleted.Count == 0)
            {
                break;
            }

            batches++;
            deleted += currentBatchDeleted.Count;
            deletedKeys.AddRange(currentBatchDeleted);

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
            CompletedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Creates the provider capabilities for the in-memory blob-store provider.
    /// </summary>
    /// <returns>The provider capability descriptor.</returns>
    /// <example>
    /// <code>
    /// var capabilities = InMemoryBlobStoreProvider.CreateCapabilities();
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
        SupportsInternalLeases = false,
        SupportsConditionalPropertiesUpdate = true,
        SupportsStreamingUpload = false,
        SupportsStreamingDownload = true,
        SupportsExpiration = true,
        SupportsRetentionSweep = true,
        SupportsNativeRetention = true
    };

    private static string CreateETag() => $"\"{Guid.NewGuid():N}\"";

    private static PropertyBag CloneProperties(PropertyBag properties) => properties?.Clone() ?? new PropertyBag();

    private static DateTimeOffset? ToUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static BlobInfo CloneInfo(BlobInfo info) => new()
    {
        Key = info.Key,
        Length = info.Length,
        ContentType = info.ContentType,
        ContentHash = info.ContentHash,
        ETag = info.ETag,
        CreatedAt = info.CreatedAt,
        LastModifiedAt = info.LastModifiedAt,
        ExpiresAt = info.ExpiresAt,
        Properties = CloneProperties(info.Properties)
    };

    private string CreateContinuationToken(string queryHash, BlobKey lastKey)
    {
        var result = BlobContinuationTokenSerializer.Serialize(new BlobContinuationToken
        {
            Provider = ProviderName,
            QueryHash = queryHash,
            Container = lastKey.Container,
            Name = lastKey.Name
        }, continuationTokenProtector);

        return result.IsSuccess ? result.Value : null;
    }
}
