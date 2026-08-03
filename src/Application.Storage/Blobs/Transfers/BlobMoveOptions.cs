// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures blob-to-blob move helpers.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobMoveOptions
/// {
///     Copy = new BlobCopyOptions()
/// };
/// </code>
/// </example>
public sealed class BlobMoveOptions
{
    /// <summary>
    /// Gets the copy options used before deleting the source.
    /// </summary>
    /// <example>
    /// <code>
    /// var copyOptions = options.Copy;
    /// </code>
    /// </example>
    public BlobCopyOptions Copy { get; init; } = new();
}
