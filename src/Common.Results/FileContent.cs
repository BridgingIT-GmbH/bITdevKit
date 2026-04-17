// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents file content information for download responses.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FileContent"/> class.
/// </remarks>
/// <param name="content">The file content as a byte array.</param>
/// <param name="fileName">The suggested file name for download.</param>
/// <param name="contentType">The MIME type of the file content.</param>
/// <param name="enableRangeProcessing">Whether to enable range processing for partial downloads.</param>
/// <param name="lastModified">The last modified date of the file for caching.</param>
/// <param name="entityTag">The entity tag for caching.</param>
public class FileContent(
    byte[] content,
    string fileName,
    string contentType,
    bool enableRangeProcessing = false,
    DateTimeOffset? lastModified = null,
    string entityTag = null)
{
    /// <summary>
    /// Gets the file content as a byte array.
    /// </summary>
    public byte[] Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>
    /// Gets the suggested file name for download.
    /// </summary>
    public string FileName { get; } = fileName ?? throw new ArgumentNullException(nameof(fileName));

    /// <summary>
    /// Gets the MIME type of the file content.
    /// </summary>
    public string ContentType { get; } = contentType ?? "application/octet-stream";

    /// <summary>
    /// Gets a value indicating whether to enable range processing for partial downloads.
    /// </summary>
    public bool EnableRangeProcessing { get; } = enableRangeProcessing;

    /// <summary>
    /// Gets the last modified date of the file for caching.
    /// </summary>
    public DateTimeOffset? LastModified { get; } = lastModified;

    /// <summary>
    /// Gets the entity tag for caching.
    /// </summary>
    public string EntityTag { get; } = entityTag;
}
