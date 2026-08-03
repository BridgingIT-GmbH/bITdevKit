// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.IO.Compression;

/// <summary>
/// Configures the blob-store compression behavior.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithCompressionBehavior(options => options.Level = CompressionLevel.SmallestSize);
/// </code>
/// </example>
public sealed class CompressionBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the GZip compression level used for uploads.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Level = CompressionLevel.Fastest;
    /// </code>
    /// </example>
    public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// Gets or sets the provider content type used for compressed bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// options.StoredContentType = ContentType.BIN;
    /// </code>
    /// </example>
    public ContentType StoredContentType { get; set; } = ContentType.BIN;
}
