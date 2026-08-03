// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures optimistic blob property patch helpers.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobPropertyPatchOptions
/// {
///     IfMatchETag = current.ETag
/// };
/// </code>
/// </example>
public sealed class BlobPropertyPatchOptions
{
    /// <summary>
    /// Gets the optional ETag to require for the update. When omitted, the current properties ETag is used.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = options.IfMatchETag;
    /// </code>
    /// </example>
    public string IfMatchETag { get; init; }

    /// <summary>
    /// Gets an optional replacement content type. When omitted, the current content type is preserved.
    /// </summary>
    /// <example>
    /// <code>
    /// var contentType = options.ContentType;
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }
}
