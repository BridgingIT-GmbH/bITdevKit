// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides provider-neutral blob transfer helpers built on the public blob client contract.
/// </summary>
/// <example>
/// <code>
/// var result = await source.CopyToAsync(sourceKey, target, targetKey);
/// </code>
/// </example>
public static class BlobTransferStorageExtensions
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Copies one blob to another blob client and key.
    /// </summary>
    /// <param name="sourceClient">The source blob client.</param>
    /// <param name="sourceKey">The source blob key.</param>
    /// <param name="targetClient">The target blob client.</param>
    /// <param name="targetKey">The target blob key.</param>
    /// <param name="options">Optional copy options.</param>
    /// <param name="cancellationToken">A token to cancel the copy.</param>
    /// <returns>A result describing the copy.</returns>
    /// <example>
    /// <code>
    /// var result = await source.CopyToAsync(sourceKey, target, targetKey);
    /// </code>
    /// </example>
    public static async Task<Result<BlobTransferResult>> CopyToAsync(
        this IBlobStoreClient sourceClient,
        BlobKey sourceKey,
        IBlobStoreClient targetClient,
        BlobKey targetKey,
        BlobCopyOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceClient is null)
        {
            return Result<BlobTransferResult>.Failure(new ArgumentError("Source blob client cannot be null."));
        }

        if (targetClient is null)
        {
            return Result<BlobTransferResult>.Failure(new ArgumentError("Target blob client cannot be null."));
        }

        var sourceValidation = BlobValidator.Validate(sourceKey);
        if (sourceValidation.IsFailure)
        {
            return Result<BlobTransferResult>.Failure(sourceValidation);
        }

        var targetValidation = BlobValidator.Validate(targetKey);
        if (targetValidation.IsFailure)
        {
            return Result<BlobTransferResult>.Failure(targetValidation);
        }

        options ??= new BlobCopyOptions();
        var downloadResult = await sourceClient.DownloadAsync(sourceKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobTransferResult>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;
        if (download?.Info is null)
        {
            return Result<BlobTransferResult>.Failure(new BlobStoreProviderError("Blob download did not include metadata."));
        }

        var expectedContentHash = options.PreserveContentHash ? download.Info.ContentHash : null;
        FileStream bufferedContent = null;
        var content = download.Content;
        try
        {
            if (!string.IsNullOrWhiteSpace(expectedContentHash) && content is { CanSeek: false })
            {
                bufferedContent = BlobBehaviorTransform.CreateTemporaryStream();
                var copy = await BlobVerifiedDownloadExtensions.CopyAndHashAsync(
                    content,
                    bufferedContent,
                    BufferSize,
                    cancellationToken).ConfigureAwait(false);
                if (copy.IsFailure)
                {
                    return Result<BlobTransferResult>.Failure(copy);
                }

                if (!string.Equals(expectedContentHash, copy.Value.Hash, StringComparison.Ordinal))
                {
                    return Result<BlobTransferResult>.Failure(new BlobStoreIntegrityError("Downloaded content hash does not match blob metadata."));
                }

                bufferedContent.Position = 0;
                content = bufferedContent;
            }

            var uploadResult = await targetClient.UploadAsync(
                new BlobUpload
                {
                    Key = targetKey,
                    Content = content,
                    ContentType = options.ContentType ?? (options.PreserveContentType ? download.Info.ContentType : null),
                    ExpectedContentHash = expectedContentHash,
                    ExpiresAt = options.ExpiresAtOverride ?? (options.PreserveExpiration ? download.Info.ExpiresAt : null),
                    Properties = CreateTargetProperties(download.Info, options),
                    OverwriteMode = options.OverwriteMode
                },
                cancellationToken).ConfigureAwait(false);
            if (uploadResult.IsFailure)
            {
                return Result<BlobTransferResult>.Failure(uploadResult);
            }

            return Result<BlobTransferResult>.Success(new BlobTransferResult
            {
                Source = download.Info,
                Target = uploadResult.Value
            });
        }
        finally
        {
            if (bufferedContent is not null)
            {
                await bufferedContent.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Moves one blob by copying it to the target and deleting the source after a successful copy.
    /// </summary>
    /// <param name="sourceClient">The source blob client.</param>
    /// <param name="sourceKey">The source blob key.</param>
    /// <param name="targetClient">The target blob client.</param>
    /// <param name="targetKey">The target blob key.</param>
    /// <param name="options">Optional move options.</param>
    /// <param name="cancellationToken">A token to cancel the move.</param>
    /// <returns>A result describing the move.</returns>
    /// <example>
    /// <code>
    /// var result = await source.MoveToAsync(sourceKey, target, targetKey);
    /// </code>
    /// </example>
    public static async Task<Result<BlobTransferResult>> MoveToAsync(
        this IBlobStoreClient sourceClient,
        BlobKey sourceKey,
        IBlobStoreClient targetClient,
        BlobKey targetKey,
        BlobMoveOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceClient is null)
        {
            return Result<BlobTransferResult>.Failure(new ArgumentError("Source blob client cannot be null."));
        }

        if (targetClient is null)
        {
            return Result<BlobTransferResult>.Failure(new ArgumentError("Target blob client cannot be null."));
        }

        var sourceValidation = BlobValidator.Validate(sourceKey);
        if (sourceValidation.IsFailure)
        {
            return Result<BlobTransferResult>.Failure(sourceValidation);
        }

        var targetValidation = BlobValidator.Validate(targetKey);
        if (targetValidation.IsFailure)
        {
            return Result<BlobTransferResult>.Failure(targetValidation);
        }

        options ??= new BlobMoveOptions();
        if (ReferenceEquals(sourceClient, targetClient) && sourceKey == targetKey)
        {
            var existing = await sourceClient.GetPropertiesAsync(sourceKey, cancellationToken).ConfigureAwait(false);
            return existing.IsFailure
                ? Result<BlobTransferResult>.Failure(existing)
                : Result<BlobTransferResult>.Success(new BlobTransferResult
                {
                    Source = existing.Value,
                    Target = existing.Value,
                    SourceDeleted = false
                });
        }

        var sourceCoordinator = StoragePermalinkExtensions.FindBlobMoveCoordinator(sourceClient);
        var targetCoordinator = StoragePermalinkExtensions.FindBlobMoveCoordinator(targetClient);
        var preservePermalink = sourceCoordinator is not null && targetCoordinator is not null && sourceCoordinator.RegistrationName == targetCoordinator.RegistrationName;
        using var sourceSuppression = preservePermalink ? sourceCoordinator.SuppressChangeTracking() : null;
        using var targetSuppression = preservePermalink ? targetCoordinator.SuppressChangeTracking() : null;
        var targetWritten = false;
        var moveTracked = false;
        try
        {
            var copyResult = await sourceClient.CopyToAsync(sourceKey, targetClient, targetKey, options.Copy, cancellationToken).ConfigureAwait(false);
            if (copyResult.IsFailure) return copyResult;
            targetWritten = true;

            var deleteResult = await sourceClient.DeleteAsync(sourceKey, new BlobDeleteOptions { IfMatchETag = copyResult.Value.Source.ETag }, cancellationToken).ConfigureAwait(false);
            if (deleteResult.IsFailure)
            {
                return Result<BlobTransferResult>.Failure(new BlobStoreTransferError("Source delete failed after the target copy succeeded.", sourceKey, targetKey, true, false, copyResult.Value.Source, copyResult.Value.Target));
            }

            if (preservePermalink)
            {
                await sourceCoordinator.TrackMoveAsync(StorageResourceLocation.ForBlob(sourceCoordinator.RegistrationName, sourceKey), StorageResourceLocation.ForBlob(targetCoordinator.RegistrationName, targetKey)).ConfigureAwait(false);
                moveTracked = true;
            }

            return Result<BlobTransferResult>.Success(new BlobTransferResult { Source = copyResult.Value.Source, Target = copyResult.Value.Target, SourceDeleted = true });
        }
        finally
        {
            if (preservePermalink && targetWritten && !moveTracked)
            {
                await targetCoordinator.TrackUpsertAsync(StorageResourceLocation.ForBlob(targetCoordinator.RegistrationName, targetKey)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Deletes all blobs matching a container and prefix.
    /// </summary>
    /// <param name="blobClient">The blob client to delete from.</param>
    /// <param name="container">The container to scan.</param>
    /// <param name="prefix">The blob name prefix.</param>
    /// <param name="options">Optional delete options.</param>
    /// <param name="cancellationToken">A token to cancel the delete operation.</param>
    /// <returns>A result describing the prefix delete operation.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DeleteByPrefixAsync("reports", "tmp/");
    /// </code>
    /// </example>
    public static async Task<Result<BlobDeletePrefixResult>> DeleteByPrefixAsync(
        this IBlobStoreClient blobClient,
        string container,
        string prefix,
        BlobDeletePrefixOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobDeletePrefixResult>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(container))
        {
            return Result<BlobDeletePrefixResult>.Failure(new BlobStoreValidationError("Container is required."));
        }

        options ??= new BlobDeletePrefixOptions();
        if (string.IsNullOrEmpty(prefix) && !options.AllowFullScan)
        {
            return Result<BlobDeletePrefixResult>.Failure(new BlobStoreQueryTooBroadError("DeleteByPrefixAsync requires a prefix unless full scan is explicitly approved."));
        }

        if (options.MaxItems is <= 0)
        {
            return Result<BlobDeletePrefixResult>.Failure(new BlobStoreValidationError("MaxItems must be greater than zero when supplied."));
        }

        var query = new BlobQuery
        {
            Container = container,
            Prefix = prefix,
            Take = options.Take,
            AllowFullScan = options.AllowFullScan
        };
        var candidates = new List<string>();
        var failures = new List<string>();
        var deleted = 0;

        await foreach (var item in blobClient.EnumerateAsync(query, new BlobEnumerationOptions { MaxItems = options.MaxItems }, cancellationToken).ConfigureAwait(false))
        {
            if (item.IsFailure)
            {
                return Result<BlobDeletePrefixResult>.Failure(item);
            }

            candidates.Add(item.Value.Key.Name);
            if (options.DryRun)
            {
                continue;
            }

            var deleteResult = await blobClient.DeleteAsync(
                    item.Value.Key,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (deleteResult.IsFailure)
            {
                var failure = $"{item.Value.Key.Name}: {CreateDetails(deleteResult)}";
                failures.Add(failure);
                if (!options.ContinueOnError)
                {
                    return Result<BlobDeletePrefixResult>.Failure(new BlobStoreTransferError(
                        $"Delete by prefix failed for '{item.Value.Key.Name}'.",
                        item.Value.Key,
                        null,
                        false,
                        false));
                }
            }
            else
            {
                deleted++;
            }
        }

        var result = new BlobDeletePrefixResult
        {
            Container = container,
            Prefix = prefix,
            DryRun = options.DryRun,
            CandidateCount = candidates.Count,
            DeletedCount = deleted,
            CandidateNames = candidates,
            Failures = failures
        };

        return failures.Count > 0
            ? Result<BlobDeletePrefixResult>.Failure(new BlobStoreTransferError(
                $"Delete by prefix completed with {failures.Count} failure(s).",
                null,
                null,
                false,
                false))
            : Result<BlobDeletePrefixResult>.Success(result);
    }

    private static PropertyBag CreateTargetProperties(BlobInfo source, BlobCopyOptions options)
    {
        var properties = options.PreserveProperties
            ? source.Properties?.Clone() ?? new PropertyBag()
            : new PropertyBag();
        if (options.Properties is not null)
        {
            properties.Merge(options.Properties);
        }

        return properties;
    }

    private static string CreateDetails(IResult result)
    {
        var errors = result.Errors?.Select(error => error.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? [];
        if (errors.Length > 0)
        {
            return string.Join("; ", errors);
        }

        return "failed";
    }
}
