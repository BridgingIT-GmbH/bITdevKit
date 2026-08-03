// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>
/// Provides provider-neutral transfer helpers between Blob Storage and File Storage.
/// </summary>
/// <example>
/// <code>
/// var upload = await blobs.UploadFileAsync(files, "exports/report.pdf", new BlobKey("reports", "report.pdf"));
/// </code>
/// </example>
public static class BlobFileStorageExtensions
{
    /// <summary>
    /// Uploads a file from an <see cref="IFileStorageProvider" /> to an <see cref="IBlobStoreClient" />.
    /// </summary>
    /// <param name="blobClient">The blob client to upload to.</param>
    /// <param name="fileProvider">The file provider to read from.</param>
    /// <param name="filePath">The source file provider path.</param>
    /// <param name="blobKey">The destination blob key.</param>
    /// <param name="options">Optional transfer options.</param>
    /// <param name="progress">Optional file progress reporter.</param>
    /// <param name="cancellationToken">A token to cancel the transfer.</param>
    /// <returns>A result containing the uploaded blob metadata.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.UploadFileAsync(files, "reports/report.pdf", new BlobKey("reports", "report.pdf"));
    /// </code>
    /// </example>
    public static async Task<Result<BlobInfo>> UploadFileAsync(
        this IBlobStoreClient blobClient,
        IFileStorageProvider fileProvider,
        string filePath,
        BlobKey blobKey,
        BlobFileUploadOptions options = null,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobInfo>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (fileProvider is null)
        {
            return Result<BlobInfo>.Failure(new ArgumentError("File provider cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<BlobInfo>.Failure(new FileSystemError("File path cannot be null or empty.", filePath));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobInfo>.Failure(keyValidation);
        }

        options ??= new BlobFileUploadOptions();

        var readResult = await fileProvider.ReadFileAsync(filePath, progress, cancellationToken).ConfigureAwait(false);
        if (readResult.IsFailure)
        {
            return Result<BlobInfo>.Failure(readResult);
        }

        await using var content = readResult.Value;
        var upload = new BlobUpload
        {
            Key = blobKey,
            Content = content,
            ContentType = options.ContentType ?? GetInferredContentType(filePath, options),
            ExpectedContentHash = options.ExpectedContentHash,
            Properties = options.Properties?.Clone() ?? new PropertyBag(),
            OverwriteMode = options.OverwriteMode
        };

        return await blobClient.UploadAsync(upload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a blob and stores it as a file through an <see cref="IFileStorageProvider" />.
    /// </summary>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKey">The source blob key.</param>
    /// <param name="fileProvider">The file provider to write to.</param>
    /// <param name="filePath">The destination file provider path.</param>
    /// <param name="options">Optional transfer options.</param>
    /// <param name="progress">Optional file progress reporter.</param>
    /// <param name="cancellationToken">A token to cancel the transfer.</param>
    /// <returns>A result describing the completed transfer.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadToFileAsync(new BlobKey("reports", "report.pdf"), files, "downloads/report.pdf");
    /// </code>
    /// </example>
    public static async Task<Result<BlobFileTransferInfo>> DownloadToFileAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        IFileStorageProvider fileProvider,
        string filePath,
        BlobFileDownloadOptions options = null,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (fileProvider is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("File provider cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<BlobFileTransferInfo>.Failure(new FileSystemError("File path cannot be null or empty.", filePath));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobFileTransferInfo>.Failure(keyValidation);
        }

        var downloadResult = await blobClient.DownloadAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobFileTransferInfo>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;
        return await download.SaveToFileAsync(
            fileProvider,
            filePath,
            options,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores an existing <see cref="BlobDownload" /> as a file through an <see cref="IFileStorageProvider" />.
    /// </summary>
    /// <param name="download">The blob download whose content stream should be written.</param>
    /// <param name="fileProvider">The file provider to write to.</param>
    /// <param name="filePath">The destination file provider path.</param>
    /// <param name="options">Optional transfer options.</param>
    /// <param name="progress">Optional file progress reporter.</param>
    /// <param name="cancellationToken">A token to cancel the transfer.</param>
    /// <returns>A result describing the completed transfer.</returns>
    /// <example>
    /// <code>
    /// await using var download = downloadResult.Value;
    /// var result = await download.SaveToFileAsync(files, "downloads/report.pdf");
    /// </code>
    /// </example>
    public static async Task<Result<BlobFileTransferInfo>> SaveToFileAsync(
        this BlobDownload download,
        IFileStorageProvider fileProvider,
        string filePath,
        BlobFileDownloadOptions options = null,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (download is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("Blob download cannot be null."));
        }

        if (fileProvider is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("File provider cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<BlobFileTransferInfo>.Failure(new FileSystemError("File path cannot be null or empty.", filePath));
        }

        if (download.Content is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("Blob download content stream cannot be null."));
        }

        if (download.Info is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("Blob download info cannot be null."));
        }

        options ??= new BlobFileDownloadOptions();

        try
        {
            var openResult = await fileProvider.OpenWriteFileAsync(
                filePath,
                options.UseTemporaryWrite,
                progress,
                cancellationToken).ConfigureAwait(false);
            if (openResult.IsFailure)
            {
                return Result<BlobFileTransferInfo>.Failure(openResult);
            }

            await using var target = openResult.Value;
            var bytesTransferred = await CopyToFileAsync(
                download.Content,
                target,
                progress,
                cancellationToken).ConfigureAwait(false);

            return Result<BlobFileTransferInfo>.Success(new BlobFileTransferInfo
            {
                Blob = download.Info,
                FilePath = filePath,
                BytesTransferred = bytesTransferred
            });
        }
        catch (OperationCanceledException)
        {
            return Result<BlobFileTransferInfo>.Failure(new OperationCancelledError("Operation cancelled during blob file transfer."));
        }
        catch (Exception ex)
        {
            return Result<BlobFileTransferInfo>.Failure(new ExceptionError(ex));
        }
    }

    private static ContentType? GetInferredContentType(string filePath, BlobFileUploadOptions options)
    {
        return options.InferContentTypeFromFileName
            ? ContentTypeExtensions.FromFileName(filePath)
            : null;
    }

    private static async Task<long> CopyToFileAsync(
        Stream source,
        Stream target,
        IProgress<FileProgress> progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return total;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            progress?.Report(new FileProgress
            {
                BytesProcessed = total,
                FilesProcessed = 0,
                TotalFiles = 1
            });
        }
    }
}
