// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides small byte-array convenience helpers for Blob Storage.
/// </summary>
/// <example>
/// <code>
/// var result = await blobs.UploadBytesAsync(new BlobKey("assets", "thumb.bin"), bytes);
/// </code>
/// </example>
public static class BlobBytesStorageExtensions
{
    /// <summary>
    /// Uploads a byte array as a blob.
    /// </summary>
    /// <param name="blobClient">The blob client to upload to.</param>
    /// <param name="blobKey">The destination blob key.</param>
    /// <param name="bytes">The bytes to upload.</param>
    /// <param name="options">Optional upload options.</param>
    /// <param name="cancellationToken">A token to cancel the upload.</param>
    /// <returns>A result containing uploaded blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.UploadBytesAsync(key, new byte[] { 1, 2, 3 });
    /// </code>
    /// </example>
    public static async Task<Result<BlobInfo>> UploadBytesAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        byte[] bytes,
        BlobBytesUploadOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobInfo>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (bytes is null)
        {
            return Result<BlobInfo>.Failure(new BlobStoreValidationError("Bytes cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(keyValidation);
        }

        options ??= new BlobBytesUploadOptions();
        await using var stream = new MemoryStream(bytes, writable: false);

        return await blobClient.UploadAsync(
            new BlobUpload
            {
                Key = blobKey,
                Content = stream,
                ContentType = options.ContentType,
                ExpectedContentHash = options.ExpectedContentHash,
                Properties = options.Properties?.Clone() ?? new PropertyBag(),
                OverwriteMode = options.OverwriteMode
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a blob into a byte array.
    /// </summary>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKey">The source blob key.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <returns>A result containing downloaded bytes and blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadBytesAsync(key);
    /// </code>
    /// </example>
    public static async Task<Result<BlobBytesContent>> DownloadBytesAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobBytesContent>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobBytesContent>.Failure(keyValidation);
        }

        var downloadResult = await blobClient.DownloadAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobBytesContent>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;

        // Providers that already hold the whole blob in a publicly visible buffer avoid a second copy.
        if (download.Content is MemoryStream source &&
            source.Position == 0 &&
            source.TryGetBuffer(out var segment) &&
            segment.Array is { } array &&
            segment.Offset == 0 &&
            segment.Count == array.Length)
        {
            return Result<BlobBytesContent>.Success(new BlobBytesContent
            {
                Info = download.Info,
                Bytes = array
            });
        }

        var capacity = download.Info?.Length is > 0 and <= int.MaxValue ? (int)download.Info.Length : 0;
        using var buffer = new MemoryStream(capacity);
        await download.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return Result<BlobBytesContent>.Success(new BlobBytesContent
        {
            Info = download.Info,
            Bytes = buffer.Length == buffer.Capacity ? buffer.GetBuffer() : buffer.ToArray()
        });
    }

    /// <summary>
    /// Downloads many blobs into byte arrays using bounded parallelism. When the provider supports batched
    /// downloads (Entity Framework), the keys are read with a few multi-key queries instead of one query per key.
    /// Every key still passes through the complete client behavior pipeline individually.
    /// All downloaded content is buffered in memory, so use it for many small blobs only.
    /// </summary>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKeys">The keys to download. A duplicate key is read once per occurrence, but only its first occurrence is batched.</param>
    /// <param name="options">Optional bulk download options.</param>
    /// <param name="cancellationToken">A token to cancel the downloads.</param>
    /// <returns>
    /// A result containing one item per requested key in the order of <paramref name="blobKeys"/>.
    /// With <see cref="BlobBytesDownloadManyOptions.ContinueOnError"/> disabled, the first failure fails the whole result instead.
    /// </returns>
    /// <example>
    /// <code>
    /// var keys = days.SelectMany(day => ids.Select(id => new BlobKey("telemetry", $"{id}/{day:yyyy-MM-dd}")));
    /// var result = await blobs.DownloadBytesManyAsync(keys, new BlobBytesDownloadManyOptions { MaxDegreeOfParallelism = 32 });
    /// var found = result.Value.Where(item => item.Result.IsSuccess);
    /// </code>
    /// </example>
    public static async Task<Result<IReadOnlyList<BlobBytesDownloadManyItem>>> DownloadBytesManyAsync(
        this IBlobStoreClient blobClient,
        IEnumerable<BlobKey> blobKeys,
        BlobBytesDownloadManyOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (blobKeys is null)
        {
            return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Failure(new BlobStoreValidationError("Blob keys cannot be null."));
        }

        options ??= new BlobBytesDownloadManyOptions();
        if (options.MaxDegreeOfParallelism <= 0)
        {
            return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Failure(
                new BlobStoreValidationError("MaxDegreeOfParallelism must be greater than zero."));
        }

        if (options.MaxConcurrentBatches <= 0)
        {
            return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Failure(
                new BlobStoreValidationError("MaxConcurrentBatches must be greater than zero."));
        }

        var keys = blobKeys.ToArray();
        foreach (var key in keys)
        {
            var keyValidation = BlobValidator.Validate(key);
            if (keyValidation.IsFailure)
            {
                return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Failure(keyValidation);
            }
        }

        var items = new BlobBytesDownloadManyItem[keys.Length];
        Result<BlobBytesContent>? firstFailure = null;
        var failureLock = new object();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var batchScope = BlobDownloadBatchScope.Begin(keys, options.MaxConcurrentBatches, linked.Token);

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, keys.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                    CancellationToken = linked.Token
                },
                async (index, token) =>
                {
                    var result = await blobClient.DownloadBytesAsync(keys[index], token).ConfigureAwait(false);
                    items[index] = new BlobBytesDownloadManyItem { Key = keys[index], Result = result };

                    if (result.IsFailure && !options.ContinueOnError)
                    {
                        lock (failureLock)
                        {
                            firstFailure ??= result;
                        }

                        await linked.CancelAsync().ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (firstFailure.HasValue && !cancellationToken.IsCancellationRequested)
        {
            // cancellation was triggered by the first failure, reported below
        }

        if (firstFailure.HasValue)
        {
            return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Failure(firstFailure.Value);
        }

        return Result<IReadOnlyList<BlobBytesDownloadManyItem>>.Success(items);
    }
}
