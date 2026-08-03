// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>
/// Configures a blob-to-object download.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobObjectDownloadOptions
/// {
///     Serializer = new SystemTextJsonSerializer()
/// };
/// </code>
/// </example>
public sealed class BlobObjectDownloadOptions
{
    /// <summary>
    /// Gets the serializer used to read the object content.
    /// </summary>
    /// <example>
    /// <code>
    /// var serializer = options.Serializer;
    /// </code>
    /// </example>
    public ISerializer Serializer { get; init; }

    /// <summary>
    /// Gets a value indicating whether binary content types are rejected for serialized object downloads.
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
