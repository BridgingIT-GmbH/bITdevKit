// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.IO.Compression;

public static class CompressionHelper
{
    private const ushort GzipLeadBytes = 0x8b1f;

    public static async Task<string> CompressAsync(string source)
    {
        if (source == null)
        {
            return null;
        }

        var result = await CompressAsync(Encoding.UTF8.GetBytes(source)).AnyContext();

        return Convert.ToBase64String(result.ToArray());
    }

    public static async Task<string> DecompressAsync(string source)
    {
        if (source == null)
        {
            return null;
        }

        var result = await DecompressAsync(Convert.FromBase64String(source)).AnyContext();

        return Encoding.UTF8.GetString(result);
    }

    public static async Task<byte[]> CompressAsync(byte[] source)
    {
        if (source == null)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await CompressAsync(sourceStream, destinationStream).AnyContext();

        return destinationStream.ToArray();
    }

    public static async Task<byte[]> DecompressAsync(byte[] source)
    {
        if (source == null)
        {
            return null;
        }

        using var sourceStream = new MemoryStream(source);
        using var destinationStream = new MemoryStream();
        await DecompressAsync(sourceStream, destinationStream).AnyContext();

        return destinationStream.ToArray();
    }

    public static async Task CompressAsync(Stream source, Stream destination)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        if (source == null)
        {
            return;
        }

        using var compressor = new GZipStream(destination, CompressionLevel.Optimal, true);
        await source.CopyToAsync(compressor).AnyContext();
        await compressor.FlushAsync();
    }

    public static async Task DecompressAsync(Stream source, Stream destination)
    {
        EnsureArg.IsNotNull(destination, nameof(destination));

        if (source == null)
        {
            return;
        }

        using var decompressor = new GZipStream(source, CompressionMode.Decompress, true);
        await decompressor.CopyToAsync(destination).AnyContext();
        await destination.FlushAsync().AnyContext();
    }

    public static bool IsCompressed(byte[] source)
    {
        if (source == null || source.Length < 2)
        {
            return false;
        }

        return BitConverter.ToUInt16(source, 0) == GzipLeadBytes;
    }
}