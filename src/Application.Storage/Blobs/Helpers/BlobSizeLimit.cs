// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides helpers for abstraction-level blob size limit enforcement.
/// </summary>
/// <example>
/// <code>
/// var validation = BlobSizeLimit.ValidateKnownLength(upload.Content, options.MaxBlobSize);
/// </code>
/// </example>
public static class BlobSizeLimit
{
    /// <summary>
    /// Validates a known stream length against the configured maximum blob size.
    /// </summary>
    /// <param name="content">The upload content stream.</param>
    /// <param name="maxBlobSize">The optional maximum blob size in bytes.</param>
    /// <returns>A success result when no known-length limit is exceeded.</returns>
    /// <example>
    /// <code>
    /// var validation = BlobSizeLimit.ValidateKnownLength(content, 1024);
    /// </code>
    /// </example>
    public static Result ValidateKnownLength(Stream content, long? maxBlobSize)
    {
        if (maxBlobSize is null || content is null || !content.CanSeek)
        {
            return Result.Success();
        }

        long remainingLength;
        try
        {
            remainingLength = content.Length - content.Position;
        }
        catch (NotSupportedException)
        {
            return Result.Success();
        }

        return remainingLength > maxBlobSize.Value
            ? Result.Failure(new BlobStoreSizeLimitExceededError(remainingLength, maxBlobSize.Value))
            : Result.Success();
    }

    /// <summary>
    /// Copies a stream while counting bytes and enforcing the configured maximum blob size.
    /// </summary>
    /// <param name="source">The readable source stream.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="maxBlobSize">The optional maximum blob size in bytes.</param>
    /// <param name="bufferSize">The read buffer size used while copying.</param>
    /// <param name="cancellationToken">The cancellation token used while copying.</param>
    /// <returns>A result containing the copied byte count, or a size-limit failure.</returns>
    /// <example>
    /// <code>
    /// var copy = await BlobSizeLimit.CopyToAsync(source, destination, maxBlobSize, cancellationToken: cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result<long>> CopyToAsync(
        Stream source,
        Stream destination,
        long? maxBlobSize,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return Result<long>.Failure(new BlobStoreValidationError("Source stream is required."));
        }

        if (destination is null)
        {
            return Result<long>.Failure(new BlobStoreValidationError("Destination stream is required."));
        }

        if (!source.CanRead)
        {
            return Result<long>.Failure(new BlobStoreValidationError("Source stream must be readable."));
        }

        if (!destination.CanWrite)
        {
            return Result<long>.Failure(new BlobStoreValidationError("Destination stream must be writable."));
        }

        if (bufferSize <= 0)
        {
            return Result<long>.Failure(new BlobStoreValidationError("Buffer size must be greater than zero."));
        }

        try
        {
            var result = await StreamHelper.CopyAsync(
                    source,
                    destination,
                    new StreamCopyOptions { BufferSize = bufferSize, MaximumBytes = maxBlobSize },
                    cancellationToken)
                .ConfigureAwait(false);
            return Result<long>.Success(result.Length);
        }
        catch (StreamSizeLimitExceededException exception)
        {
            return Result<long>.Failure(new BlobStoreSizeLimitExceededError(exception.ObservedBytes, exception.MaximumBytes));
        }
    }
}
