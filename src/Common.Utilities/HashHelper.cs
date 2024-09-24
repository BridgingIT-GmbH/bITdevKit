// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

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

        return BitConverter.ToString(MD5.HashData(input)).Replace("-", string.Empty).ToLowerInvariant();
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