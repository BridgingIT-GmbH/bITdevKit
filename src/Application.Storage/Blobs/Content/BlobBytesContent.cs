// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents downloaded byte content and its blob metadata.
/// </summary>
/// <example>
/// <code>
/// var bytes = result.Value.Bytes;
/// var info = result.Value.Info;
/// </code>
/// </example>
public sealed class BlobBytesContent
{
    /// <summary>
    /// Gets the blob information returned with the byte content.
    /// </summary>
    /// <example>
    /// <code>
    /// var info = content.Info;
    /// </code>
    /// </example>
    public BlobInfo Info { get; init; }

    /// <summary>
    /// Gets the downloaded bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = content.Bytes.Length;
    /// </code>
    /// </example>
    public byte[] Bytes { get; init; } = [];
}
