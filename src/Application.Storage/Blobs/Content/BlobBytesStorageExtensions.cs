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
        using var buffer = new MemoryStream();
        await download.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return Result<BlobBytesContent>.Success(new BlobBytesContent
        {
            Info = download.Info,
            Bytes = buffer.ToArray()
        });
    }
}
