// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures a blob-to-file transfer.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobFileDownloadOptions
/// {
///     UseTemporaryWrite = true
/// };
/// </code>
/// </example>
public sealed class BlobFileDownloadOptions
{
    /// <summary>
    /// Gets a value indicating whether the file provider should stage the write and publish it on successful close.
    /// </summary>
    /// <example>
    /// <code>
    /// var staged = options.UseTemporaryWrite;
    /// </code>
    /// </example>
    public bool UseTemporaryWrite { get; init; } = true;
}
