// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using System.Security.Cryptography;

/// <summary>
/// Provides reusable stream operations.
/// </summary>
/// <example>
/// <code>
/// var result = await StreamHelper.CopyAsync(
///     source,
///     destination,
///     new StreamCopyOptions { MaximumBytes = ByteSize.Megabytes(1), HashAlgorithm = HashAlgorithmName.SHA256 },
///     cancellationToken);
/// </code>
/// </example>
public static class StreamHelper
{
    /// <summary>
    /// Copies the source from its current position to the destination without disposing either stream.
    /// </summary>
    /// <param name="source">The readable source stream.</param>
    /// <param name="destination">The writable destination stream.</param>
    /// <param name="options">The optional copy settings.</param>
    /// <param name="cancellationToken">The token used to cancel the copy.</param>
    /// <returns>The copied byte count and optional lowercase hexadecimal hash.</returns>
    /// <exception cref="StreamSizeLimitExceededException">The source exceeds the configured maximum.</exception>
    /// <example>
    /// <code>
    /// var result = await StreamHelper.CopyAsync(source, destination, cancellationToken: cancellationToken);
    /// </code>
    /// </example>
    public static async Task<StreamCopyResult> CopyAsync(
        Stream source,
        Stream destination,
        StreamCopyOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (!source.CanRead)
        {
            throw new ArgumentException("The source stream must be readable.", nameof(source));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        options ??= new StreamCopyOptions();
        ValidateOptions(options);

        using var hash = options.HashAlgorithm.HasValue
            ? IncrementalHash.CreateHash(options.HashAlgorithm.Value)
            : null;
        var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
        long copied = 0;

        try
        {
            while (true)
            {
                var read = await source.ReadAsync(
                        buffer.AsMemory(0, options.BufferSize),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (options.MaximumBytes.HasValue && copied + read > options.MaximumBytes.Value)
                {
                    var allowed = checked((int)Math.Max(0, options.MaximumBytes.Value - copied));
                    if (allowed > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, allowed), cancellationToken).ConfigureAwait(false);
                        hash?.AppendData(buffer.AsSpan(0, allowed));
                        copied += allowed;
                    }

                    throw new StreamSizeLimitExceededException(options.MaximumBytes.Value, checked(copied + read - allowed));
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash?.AppendData(buffer.AsSpan(0, read));
                copied += read;
            }

            return new StreamCopyResult(
                copied,
                hash is null ? null : Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateOptions(StreamCopyOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BufferSize);
        if (options.MaximumBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumBytes), "Maximum bytes must be greater than or equal to zero.");
        }

        if (options.HashAlgorithm.HasValue && string.IsNullOrWhiteSpace(options.HashAlgorithm.Value.Name))
        {
            throw new ArgumentException("Hash algorithm must have a name.", nameof(options.HashAlgorithm));
        }
    }
}

/// <summary>
/// Configures a stream copy operation.
/// </summary>
/// <example>
/// <code>
/// var options = new StreamCopyOptions { MaximumBytes = ByteSize.Megabytes(4) };
/// </code>
/// </example>
public sealed class StreamCopyOptions
{
    /// <summary>
    /// Gets the read buffer size.
    /// </summary>
    /// <example>
    /// <code>
    /// var size = options.BufferSize;
    /// </code>
    /// </example>
    public int BufferSize { get; init; } = 81920;

    /// <summary>
    /// Gets the optional maximum number of bytes written to the destination.
    /// </summary>
    /// <example>
    /// <code>
    /// var maximum = options.MaximumBytes;
    /// </code>
    /// </example>
    public long? MaximumBytes { get; init; }

    /// <summary>
    /// Gets the optional incremental hash algorithm.
    /// </summary>
    /// <example>
    /// <code>
    /// var algorithm = options.HashAlgorithm;
    /// </code>
    /// </example>
    public HashAlgorithmName? HashAlgorithm { get; init; }
}

/// <summary>
/// Describes a completed stream copy.
/// </summary>
/// <param name="Length">The copied byte count.</param>
/// <param name="Hash">The optional lowercase hexadecimal hash.</param>
/// <example>
/// <code>
/// var bytes = result.Length;
/// </code>
/// </example>
public sealed record StreamCopyResult(long Length, string Hash);

/// <summary>
/// Indicates that a stream exceeded an enforced byte limit.
/// </summary>
/// <example>
/// <code>
/// catch (StreamSizeLimitExceededException exception)
/// {
///     logger.LogWarning("Stream exceeded {MaximumBytes} bytes", exception.MaximumBytes);
/// }
/// </code>
/// </example>
public sealed class StreamSizeLimitExceededException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamSizeLimitExceededException" /> class.
    /// </summary>
    /// <param name="maximumBytes">The configured maximum.</param>
    /// <param name="observedBytes">The observed byte count when the limit was detected.</param>
    /// <example>
    /// <code>
    /// throw new StreamSizeLimitExceededException(1024, 1025);
    /// </code>
    /// </example>
    public StreamSizeLimitExceededException(long maximumBytes, long observedBytes)
        : base($"Stream exceeds the maximum size of {maximumBytes} bytes.")
    {
        this.MaximumBytes = maximumBytes;
        this.ObservedBytes = observedBytes;
    }

    /// <summary>
    /// Gets the configured maximum byte count.
    /// </summary>
    /// <example>
    /// <code>
    /// var maximum = exception.MaximumBytes;
    /// </code>
    /// </example>
    public long MaximumBytes { get; }

    /// <summary>
    /// Gets the observed byte count when the limit was detected.
    /// </summary>
    /// <example>
    /// <code>
    /// var observed = exception.ObservedBytes;
    /// </code>
    /// </example>
    public long ObservedBytes { get; }
}
