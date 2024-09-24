// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.IO.Compression;

/// <summary>
///     Provides methods for compressing and decompressing data asynchronously.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    ///     Represents the lead bytes in a GZip file header used to identify GZip compressed data.
    /// </summary>
    private const ushort GzipLeadBytes = 0x8b1f;

    /// <summary>
    ///     Compresses the given string asynchronously.
    /// </summary>
    /// <param name="source">The string to compress.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the compressed string in Base64
    ///     format, or null if the source is null.
    /// </returns>
    public static async Task<string> CompressAsync(string source)
    {
        if (source is null)
        {
            return null;
        }

        var result = await CompressAsync(Encoding.UTF8.GetBytes(source)).AnyContext();

        return Convert.ToBase64String(result.ToArray());
    }

    /// <summary>
    ///     Decompresses a Base64 encoded and compressed string asynchronously.
    /// </summary>
    /// <param name="source">The Base64 encoded and compressed string to decompress.</param>
    /// <returns>
    ///     A task that represents the asynchronous decompression operation. The task result contains the decompressed
    ///     string, or null if the input is null.
    /// </returns>
    public static async Task<string> DecompressAsync(string source)
    {
        if (source is null)
        {
            return null;
        }

        var result = await DecompressAsync(Convert.FromBase64String(source)).AnyContext();

        return Encoding.UTF8.GetString(result);
    }

    /// <summary>
    ///     Compresses a given string using GZip compression and returns the compressed data as a Base64 encoded string.
    /// </summary>
    /// <param name="source">The string to be compressed.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The result of the task contains the compressed string in
    ///     Base64 format, or null if the input was null.
    /// </returns>
    public static async Task<byte[]> CompressAsync(byte[] source)
    {
        if (source is null)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await CompressAsync(sourceStream, destinationStream).AnyContext();

        return destinationStream.ToArray();
    }

    /// <summary>
    ///     Decompresses a Base64 encoded compressed string asynchronously.
    /// </summary>
    /// <param name="source">The Base64 encoded compressed string to decompress.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the decompressed string.</returns>
    public static async Task<byte[]> DecompressAsync(byte[] source)
    {
        if (source is null)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await DecompressAsync(sourceStream, destinationStream).AnyContext();

        return destinationStream.ToArray();
    }

    /// <summary>
    ///     Compresses the input stream asynchronously using gzip compression and writes the compressed data to the destination
    ///     stream.
    /// </summary>
    /// <param name="source">The input stream to be compressed.</param>
    /// <param name="destination">The stream where the compressed data will be written to.</param>
    /// <returns>A task that represents the asynchronous compression operation.</returns>
    public static async Task CompressAsync(Stream source, Stream destination)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        if (source is null)
        {
            return;
        }

        await using var compressor = new GZipStream(destination, CompressionLevel.Optimal, true);
        await source.CopyToAsync(compressor).AnyContext();
        await compressor.FlushAsync();
    }

    /// <summary>
    ///     Decompresses the given Base64 encoded string asynchronously.
    /// </summary>
    /// <param name="source">The Base64 encoded string to decompress.</param>
    /// <param name="destination">The stream where the compressed data will be written to.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains the decompressed string, or null if
    ///     the source is null.
    /// </returns>
    public static async Task DecompressAsync(Stream source, Stream destination)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        if (source is null)
        {
            return;
        }

        using var decompressor = new GZipStream(source, CompressionMode.Decompress, true);
        await decompressor.CopyToAsync(destination).AnyContext();
        await destination.FlushAsync().AnyContext();
    }

    /// <summary>
    ///     Determines if a given byte array is compressed using gzip format.
    /// </summary>
    /// <param name="source">The byte array to check for compression.</param>
    /// <returns>True if the byte array is compressed, otherwise false.</returns>
    public static bool IsCompressed(byte[] source)
    {
        if (source is null || source.Length < 2)
        {
            return false;
        }

        return BitConverter.ToUInt16(source, 0) == GzipLeadBytes;
    }
}