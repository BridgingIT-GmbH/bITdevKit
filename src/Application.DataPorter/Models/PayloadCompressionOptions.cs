// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using System.IO.Compression;

/// <summary>
/// Defines payload compression or packaging settings for DataPorter operations.
/// </summary>
public sealed record PayloadCompressionOptions
{
    /// <summary>
    /// Gets a reusable instance representing no compression.
    /// </summary>
    public static PayloadCompressionOptions None { get; } = new();

    /// <summary>
    /// Gets or sets the compression or packaging kind to apply.
    /// </summary>
    public PayloadCompressionKind Kind { get; init; } = PayloadCompressionKind.None;

    /// <summary>
    /// Gets or sets the compression level to use when compression is enabled.
    /// </summary>
    public CompressionLevel? CompressionLevel { get; init; }

    /// <summary>
    /// Gets or sets the ZIP entry name to use when <see cref="Kind"/> is <see cref="PayloadCompressionKind.Zip"/>.
    /// </summary>
    public string ZipEntryName { get; init; }
}
