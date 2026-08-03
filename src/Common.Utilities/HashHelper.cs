// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
///     Provides utility methods to compute MD5 hashes from various input types.
/// </summary>
public static class HashHelper
{
    /// <summary>
    ///     Computes the MD5 hash of the specified byte array.
    /// </summary>
    /// <param name="input">The input byte array to hash.</param>
    /// <returns>A string representation of the computed MD5 hash. If input is null, returns an empty string.</returns>
    public static string Compute(byte[] input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return Convert.ToHexStringLower(MD5.HashData(input));
    }

    /// <summary>
    ///     Computes the SHA-256 hash of the specified byte array.
    /// </summary>
    /// <param name="input">The input byte array to hash.</param>
    /// <returns>A string representation of the computed SHA-256 hash. If input is null, returns an empty string.</returns>
    public static string ComputeSha256(byte[] input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return Convert.ToHexStringLower(SHA256.HashData(input));
    }

    /// <summary>
    ///     Computes the MD5 hash of the given stream.
    /// </summary>
    /// <param name="stream">The input stream to hash.</param>
    /// <returns>
    ///     A string representation of the computed MD5 hash in lowercase hexadecimal format. If the stream is null,
    ///     returns an empty string.
    /// </returns>
    public static string Compute(Stream stream)
    {
        if (stream is null)
        {
            return string.Empty;
        }

        using var ms = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(ms);

        return Compute(ms.ToArray());
    }

    /// <summary>
    ///     Computes the SHA-256 hash of the given stream.
    /// </summary>
    /// <param name="stream">The input stream to hash.</param>
    /// <returns>
    ///     A string representation of the computed SHA-256 hash in lowercase hexadecimal format. If the stream is null,
    ///     returns an empty string.
    /// </returns>
    public static string ComputeSha256(Stream stream)
    {
        if (stream is null)
        {
            return string.Empty;
        }

        using var ms = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(ms);

        return ComputeSha256(ms.ToArray());
    }

    /// <summary>
    ///     Computes the SHA-256 hash of the given stream incrementally from its current position.
    /// </summary>
    /// <param name="stream">The input stream to hash.</param>
    /// <param name="bufferSize">The read buffer size used while hashing.</param>
    /// <param name="cancellationToken">The cancellation token used while reading the stream.</param>
    /// <returns>
    ///     A lowercase hexadecimal SHA-256 hash. If the stream is null, returns an empty string.
    /// </returns>
    public static async Task<string> ComputeSha256Async(
        Stream stream,
        int bufferSize = 81920,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            return string.Empty;
        }

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than zero.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Computes the MD5 hash of a byte array and returns it as a hexadecimal string.
    /// </summary>
    /// <param name="input">The byte array for which to compute the hash.</param>
    /// <returns>The hexadecimal string representation of the MD5 hash.</returns>
    public static string Compute(string input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return Compute(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>
    ///     Computes the SHA-256 hash of a string and returns it as a hexadecimal string.
    /// </summary>
    /// <param name="input">The input string for which to compute the hash.</param>
    /// <returns>The hexadecimal string representation of the SHA-256 hash.</returns>
    public static string ComputeSha256(string input)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return ComputeSha256(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>
    ///     Computes the hash of the given input object.
    /// </summary>
    /// <param name="input">The object to compute the hash for.</param>
    /// <param name="serializer">
    ///     Optional serializer to serialize the object to bytes. If not provided, the object is directly
    ///     serialized to bytes.
    /// </param>
    /// <returns>A string representation of the computed hash, or an empty string if the input is null.</returns>
    public static string Compute(object input, ISerializer serializer = null)
    {
        if (input is null)
        {
            return string.Empty;
        }

        return Compute(SerializeToBytes(input));
    }

    /// <summary>
    ///     Serializes the given object to a byte array.
    /// </summary>
    /// <param name="input">The object to be serialized.</param>
    /// <returns>A byte array representing the serialized object, or null if the input is null.</returns>
    private static byte[] SerializeToBytes(object input)
    {
        if (input is null)
        {
            return null;
        }

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input));
    }
}
