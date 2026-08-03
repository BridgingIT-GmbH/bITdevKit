// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures verified blob download helpers.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobDownloadVerificationOptions
/// {
///     AllowMissingContentHash = false
/// };
/// </code>
/// </example>
public sealed class BlobDownloadVerificationOptions
{
    /// <summary>
    /// Gets a value indicating whether downloads without a stored content hash are allowed.
    /// </summary>
    /// <example>
    /// <code>
    /// var allowMissing = options.AllowMissingContentHash;
    /// </code>
    /// </example>
    public bool AllowMissingContentHash { get; init; }

    /// <summary>
    /// Gets the copy buffer size in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var bufferSize = options.BufferSize;
    /// </code>
    /// </example>
    public int BufferSize { get; init; } = 81920;
}
