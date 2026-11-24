// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.IO.Compression;

/// <summary>Provides conservative resource-admission reservations for built-in streaming compression.</summary>
/// <remarks>Reservations include native codec workspace. They are accounting estimates rather than measured heap usage.</remarks>
/// <example><code>var reservation = CacheCompressionWorkspace.Encode(CacheCompressionAlgorithm.Brotli, CompressionLevel.Fastest);</code></example>
public static class CacheCompressionWorkspace
{
    // BCL Brotli uses native ring buffers, command/hash tables and quality-dependent search state.
    // These accounting reservations deliberately exceed the streaming buffers visible to managed code.
    /// <summary>The conservative byte reservation for one Brotli decode operation.</summary>
    public const long BrotliDecode = 32 * CacheDefaults.MiB;

    /// <summary>Gets the workspace reservation for one selected compression writer.</summary>
    /// <param name="algorithm">The effective compression algorithm.</param>
    /// <param name="level">The selected BCL compression level.</param>
    /// <returns>The reserved workspace in bytes, excluding separate transfer buffers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A Brotli compression level is not supported.</exception>
    public static long Encode(CacheCompressionAlgorithm algorithm, CompressionLevel level) => algorithm != CacheCompressionAlgorithm.Brotli ? CacheDefaults.MiB : level switch
    {
        CompressionLevel.NoCompression or CompressionLevel.Fastest => 48 * CacheDefaults.MiB,
        CompressionLevel.Optimal => 256 * CacheDefaults.MiB,
        CompressionLevel.SmallestSize => 512 * CacheDefaults.MiB,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };
}