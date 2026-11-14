// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.IO.Compression;

/// <summary>Encodes and decodes the version-one gzip representation using forward-only BCL streams.</summary>
/// <param name="compressionLevel">The compression level used for encoding. Decoding reads every supported gzip level.</param>
/// <param name="leaveOpen">Whether disposing the encoder or decoder leaves its supplied stream open.</param>
/// <remarks>This format accepts no parameters. It does not impose a logical-output limit or verify an application envelope.
/// Callers enforce their own size, integrity, ownership and deadline contracts.</remarks>
/// <example><code>var transform = new GZipPayloadTransform(CompressionLevel.Fastest);
/// await using var encoder = transform.CreateEncodingStream(destination, ReadOnlyMemory&lt;byte&gt;.Empty);
/// await source.CopyToAsync(encoder, cancellationToken);</code></example>
public sealed class GZipPayloadTransform(CompressionLevel compressionLevel = CompressionLevel.Optimal, bool leaveOpen = true) : IStreamingPayloadTransform
{
    private readonly CompressionLevel compressionLevel = Validate(compressionLevel);
    private readonly bool leaveOpen = leaveOpen;

    /// <inheritdoc />
    public string FormatId => "gzip";

    /// <inheritdoc />
    public int FormatVersion => 1;

    /// <inheritdoc />
    /// <remarks>The one-MiB estimate covers gzip workspace. Caller-owned transfer buffers and output are separate charges.</remarks>
    public long MaximumStateBytes => 1024L * 1024;

    /// <inheritdoc />
    public Stream CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ValidateParameters(parameters);
        return new GZipStream(destination, this.compressionLevel, this.leaveOpen);
    }

    /// <inheritdoc />
    public Stream CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateParameters(parameters);
        return new GZipStream(source, CompressionMode.Decompress, this.leaveOpen);
    }

    private static CompressionLevel Validate(CompressionLevel level) => Enum.IsDefined(level)
        ? level : throw new ArgumentOutOfRangeException(nameof(level));

    private static void ValidateParameters(ReadOnlyMemory<byte> parameters)
    {
        if (!parameters.IsEmpty)
        {
            throw new ArgumentException("The gzip format does not accept parameters.", nameof(parameters));
        }
    }
}