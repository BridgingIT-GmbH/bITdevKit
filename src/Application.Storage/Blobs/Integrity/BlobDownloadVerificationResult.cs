// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a completed verified blob download.
/// </summary>
/// <example>
/// <code>
/// var hash = result.Value.CalculatedContentHash;
/// </code>
/// </example>
public sealed class BlobDownloadVerificationResult
{
    /// <summary>
    /// Gets the downloaded blob metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var info = result.Blob;
    /// </code>
    /// </example>
    public BlobInfo Blob { get; init; }

    /// <summary>
    /// Gets the total number of bytes copied to the destination stream.
    /// </summary>
    /// <example>
    /// <code>
    /// var bytes = result.BytesTransferred;
    /// </code>
    /// </example>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Gets the SHA-256 content hash calculated from the downloaded bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var hash = result.CalculatedContentHash;
    /// </code>
    /// </example>
    public string CalculatedContentHash { get; init; }
}
