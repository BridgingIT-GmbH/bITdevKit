// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Security.Cryptography;

/// <summary>
/// Provides streaming verified download helpers for Blob Storage.
/// </summary>
/// <example>
/// <code>
/// var result = await blobs.DownloadVerifiedToAsync(key, destination);
/// </code>
/// </example>
public static class BlobVerifiedDownloadExtensions
{
    /// <summary>
    /// Downloads a blob to a caller-owned stream and verifies the downloaded bytes against <see cref="BlobInfo.ContentHash" />.
    /// </summary>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKey">The source blob key.</param>
    /// <param name="destination">The writable destination stream owned by the caller.</param>
    /// <param name="options">Optional verification options.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <returns>A result describing the verified transfer.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadVerifiedToAsync(key, outputStream);
    /// </code>
    /// </example>
    public static async Task<Result<BlobDownloadVerificationResult>> DownloadVerifiedToAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        Stream destination,
        BlobDownloadVerificationOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new ArgumentError("Blob client cannot be null."));
        }

        if (destination is null)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreValidationError("Destination stream is required."));
        }

        if (!destination.CanWrite)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreValidationError("Destination stream must be writable."));
        }

        var keyValidation = BlobValidator.Validate(blobKey);
        if (keyValidation.IsFailure)
        {
            return Result<BlobDownloadVerificationResult>.Failure(keyValidation);
        }

        options ??= new BlobDownloadVerificationOptions();
        if (options.BufferSize <= 0)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreValidationError("BufferSize must be greater than zero."));
        }

        var downloadResult = await blobClient.DownloadAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobDownloadVerificationResult>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;
        if (download?.Info is null)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreProviderError("Blob download did not include metadata."));
        }

        if (string.IsNullOrWhiteSpace(download.Info?.ContentHash) && !options.AllowMissingContentHash)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreIntegrityError("Blob content hash is required for verified download."));
        }

        var copyResult = await CopyAndHashAsync(download.Content, destination, options.BufferSize, cancellationToken).ConfigureAwait(false);
        if (copyResult.IsFailure)
        {
            return Result<BlobDownloadVerificationResult>.Failure(copyResult);
        }

        if (!string.IsNullOrWhiteSpace(download.Info.ContentHash) &&
            !string.Equals(download.Info.ContentHash, copyResult.Value.Hash, StringComparison.Ordinal))
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreIntegrityError("Downloaded content hash does not match blob metadata."));
        }

        return Result<BlobDownloadVerificationResult>.Success(new BlobDownloadVerificationResult
        {
            Blob = download.Info,
            BytesTransferred = copyResult.Value.BytesTransferred,
            CalculatedContentHash = copyResult.Value.Hash
        });
    }

    /// <summary>
    /// Downloads a blob to file storage and verifies the downloaded bytes against <see cref="BlobInfo.ContentHash" />.
    /// </summary>
    /// <param name="blobClient">The blob client to download from.</param>
    /// <param name="blobKey">The source blob key.</param>
    /// <param name="fileProvider">The file provider to write to.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="downloadOptions">Optional file download options.</param>
    /// <param name="verificationOptions">Optional verification options.</param>
    /// <param name="progress">Optional file progress reporter.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <returns>A result describing the file transfer.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.DownloadVerifiedToFileAsync(key, files, "downloads/report.pdf");
    /// </code>
    /// </example>
    public static async Task<Result<BlobFileTransferInfo>> DownloadVerifiedToFileAsync(
        this IBlobStoreClient blobClient,
        BlobKey blobKey,
        IFileStorageProvider fileProvider,
        string filePath,
        BlobFileDownloadOptions downloadOptions = null,
        BlobDownloadVerificationOptions verificationOptions = null,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (fileProvider is null)
        {
            return Result<BlobFileTransferInfo>.Failure(new ArgumentError("File provider cannot be null."));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<BlobFileTransferInfo>.Failure(new FileSystemError("File path cannot be null or empty.", filePath));
        }

        var temporaryPath = CreateTemporaryPath(filePath);
        var result = await DownloadVerifiedToTemporaryFileAsync(
            blobClient,
            blobKey,
            fileProvider,
            temporaryPath,
            verificationOptions,
            progress,
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            await fileProvider.DeleteFileAsync(temporaryPath, null, cancellationToken).ConfigureAwait(false);

            return Result<BlobFileTransferInfo>.Failure(result);
        }

        var moveResult = await fileProvider.MoveFileAsync(temporaryPath, filePath, progress, cancellationToken).ConfigureAwait(false);
        if (moveResult.IsFailure)
        {
            await fileProvider.DeleteFileAsync(temporaryPath, null, cancellationToken).ConfigureAwait(false);

            return Result<BlobFileTransferInfo>.Failure(moveResult);
        }

        progress?.Report(new FileProgress
        {
            BytesProcessed = result.Value.BytesTransferred,
            FilesProcessed = 1,
            TotalFiles = 1
        });

        return Result<BlobFileTransferInfo>.Success(new BlobFileTransferInfo
        {
            Blob = result.Value.Blob,
            FilePath = filePath,
            BytesTransferred = result.Value.BytesTransferred
        });
    }

    private static async Task<Result<BlobDownloadVerificationResult>> DownloadVerifiedToTemporaryFileAsync(
        IBlobStoreClient blobClient,
        BlobKey blobKey,
        IFileStorageProvider fileProvider,
        string temporaryPath,
        BlobDownloadVerificationOptions verificationOptions,
        IProgress<FileProgress> progress,
        CancellationToken cancellationToken)
    {
        var downloadResult = await blobClient.DownloadAsync(blobKey, cancellationToken).ConfigureAwait(false);
        if (downloadResult.IsFailure)
        {
            return Result<BlobDownloadVerificationResult>.Failure(downloadResult);
        }

        await using var download = downloadResult.Value;
        if (download?.Info is null)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreProviderError("Blob download did not include metadata."));
        }

        verificationOptions ??= new BlobDownloadVerificationOptions();
        if (verificationOptions.BufferSize <= 0)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreValidationError("BufferSize must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(download.Info.ContentHash) && !verificationOptions.AllowMissingContentHash)
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreIntegrityError("Blob content hash is required for verified download."));
        }

        var verifyingStream = new HashingReadStream(download.Content, verificationOptions.BufferSize);
        var writeResult = await fileProvider.WriteFileAsync(temporaryPath, verifyingStream, progress, cancellationToken).ConfigureAwait(false);
        if (writeResult.IsFailure)
        {
            return Result<BlobDownloadVerificationResult>.Failure(writeResult);
        }

        if (!string.IsNullOrWhiteSpace(download.Info.ContentHash) &&
            !string.Equals(download.Info.ContentHash, verifyingStream.CalculatedContentHash, StringComparison.Ordinal))
        {
            return Result<BlobDownloadVerificationResult>.Failure(new BlobStoreIntegrityError("Downloaded content hash does not match blob metadata."));
        }

        return Result<BlobDownloadVerificationResult>.Success(new BlobDownloadVerificationResult
        {
            Blob = download.Info,
            BytesTransferred = verifyingStream.BytesRead,
            CalculatedContentHash = verifyingStream.CalculatedContentHash
        });
    }

    /// <summary>
    /// Copies a readable stream to a destination stream while calculating the blob content hash.
    /// </summary>
    /// <param name="source">The source stream to read.</param>
    /// <param name="destination">The destination stream to write.</param>
    /// <param name="bufferSize">The copy buffer size in bytes.</param>
    /// <param name="cancellationToken">A token to cancel the copy.</param>
    /// <returns>A result containing the number of transferred bytes and calculated hash.</returns>
    /// <example>
    /// <code>
    /// var result = await BlobVerifiedDownloadExtensions.CopyAndHashAsync(source, destination, 81920, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result<VerifiedCopyResult>> CopyAndHashAsync(
        Stream source,
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken)
    {
        if (source is null)
        {
            return Result<VerifiedCopyResult>.Failure(new BlobStoreValidationError("Source stream is required."));
        }

        if (!source.CanRead)
        {
            return Result<VerifiedCopyResult>.Failure(new BlobStoreValidationError("Source stream must be readable."));
        }

        if (destination is null || !destination.CanWrite)
        {
            return Result<VerifiedCopyResult>.Failure(new BlobStoreValidationError("Destination stream must be writable."));
        }

        if (bufferSize <= 0)
        {
            return Result<VerifiedCopyResult>.Failure(new BlobStoreValidationError("Buffer size must be greater than zero."));
        }

        var result = await StreamHelper.CopyAsync(
                source,
                destination,
                new StreamCopyOptions { BufferSize = bufferSize, HashAlgorithm = HashAlgorithmName.SHA256 },
                cancellationToken)
            .ConfigureAwait(false);
        return Result<VerifiedCopyResult>.Success(new VerifiedCopyResult(
            result.Length,
            $"{BlobContentHash.Prefix}{result.Hash}"));
    }

    private static string FormatHash(byte[] hash) =>
        $"{BlobContentHash.Prefix}{Convert.ToHexStringLower(hash)}";

    private static string CreateTemporaryPath(string filePath)
    {
        var slashIndex = filePath.LastIndexOfAny(['/', '\\']);
        if (slashIndex < 0)
        {
            return $".{filePath}.{Guid.NewGuid():N}.tmp";
        }

        var directory = filePath[..(slashIndex + 1)];
        var name = filePath[(slashIndex + 1)..];

        return $"{directory}.{name}.{Guid.NewGuid():N}.tmp";
    }

    /// <summary>
    /// Describes a completed copy-and-hash operation.
    /// </summary>
    /// <param name="BytesTransferred">The number of bytes copied from source to destination.</param>
    /// <param name="Hash">The calculated blob content hash.</param>
    /// <example>
    /// <code>
    /// var bytes = result.BytesTransferred;
    /// var hash = result.Hash;
    /// </code>
    /// </example>
    public sealed record VerifiedCopyResult(long BytesTransferred, string Hash);

    private sealed class HashingReadStream(Stream inner, int bufferSize) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private bool hashFinalized;

        public long BytesRead { get; private set; }

        public string CalculatedContentHash { get; private set; }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, Math.Min(count, bufferSize));
            this.Append(buffer.AsSpan(offset, read));

            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, bufferSize)], cancellationToken).ConfigureAwait(false);
            this.Append(buffer.Span[..read]);

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private void Append(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                if (!this.hashFinalized)
                {
                    this.CalculatedContentHash = FormatHash(this.hash.GetHashAndReset());
                    this.hashFinalized = true;
                }

                return;
            }

            this.hash.AppendData(bytes);
            this.BytesRead += bytes.Length;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.hash.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
