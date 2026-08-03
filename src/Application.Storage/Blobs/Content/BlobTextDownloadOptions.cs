// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;

/// <summary>
/// Configures a blob-to-text download.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobTextDownloadOptions
/// {
///     RequireTextContentType = true
/// };
/// </code>
/// </example>
public sealed class BlobTextDownloadOptions
{
    /// <summary>
    /// Gets the text encoding used to decode the blob content stream.
    /// </summary>
    /// <example>
    /// <code>
    /// var encoding = options.Encoding;
    /// </code>
    /// </example>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// Gets a value indicating whether binary content types are rejected for text downloads.
    /// </summary>
    /// <example>
    /// <code>
    /// var rejectBinary = options.RejectBinaryContentType;
    /// </code>
    /// </example>
    public bool RejectBinaryContentType { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the downloaded blob must have a known text content type.
    /// </summary>
    /// <example>
    /// <code>
    /// var requireText = options.RequireTextContentType;
    /// </code>
    /// </example>
    public bool RequireTextContentType { get; init; }
}
