// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines a single-encode, same-store optional compression candidate.</summary>
/// <example><code>var options = new CacheCompressionOptions();</code></example>
public sealed record CacheCompressionOptions
{
    /// <summary>Gets whether compression is explicitly enabled.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>Gets required explicit GZip or Brotli when enabled.</summary>
    public CacheCompressionAlgorithm? Algorithm { get; init; } = null;

    /// <summary>Gets required explicit BCL compression level when enabled.</summary>
    public System.IO.Compression.CompressionLevel? Level { get; init; } = null;

    /// <summary>Gets minimum encoded bytes before attempting a compressed candidate.</summary>
    public long ThresholdBytes { get; init; } = 4 * CacheDefaults.KiB;

    /// <summary>Gets minimum storage saving required to keep the candidate.</summary>
    public double MinimumSavingRatio { get; init; } = 0.1;
}