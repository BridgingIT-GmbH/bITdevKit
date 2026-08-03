// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures blob-to-blob copy helpers.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobCopyOptions
/// {
///     PreserveProperties = true
/// };
/// </code>
/// </example>
public sealed class BlobCopyOptions
{
    /// <summary>
    /// Gets a value indicating whether source properties should be copied.
    /// </summary>
    /// <example>
    /// <code>
    /// var preserve = options.PreserveProperties;
    /// </code>
    /// </example>
    public bool PreserveProperties { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether source content type should be copied.
    /// </summary>
    /// <example>
    /// <code>
    /// var preserve = options.PreserveContentType;
    /// </code>
    /// </example>
    public bool PreserveContentType { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the source content hash should be supplied as the expected target hash.
    /// </summary>
    /// <example>
    /// <code>
    /// var preserve = options.PreserveContentHash;
    /// </code>
    /// </example>
    public bool PreserveContentHash { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the source expiration should be copied when no override is supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var preserve = options.PreserveExpiration;
    /// </code>
    /// </example>
    public bool PreserveExpiration { get; init; } = true;

    /// <summary>
    /// Gets an optional target expiration override.
    /// </summary>
    /// <example>
    /// <code>
    /// var expiresAt = options.ExpiresAtOverride;
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAtOverride { get; init; }

    /// <summary>
    /// Gets an optional content type override.
    /// </summary>
    /// <example>
    /// <code>
    /// var contentType = options.ContentType;
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets custom properties to merge into the target upload after source properties are copied.
    /// </summary>
    /// <example>
    /// <code>
    /// var properties = options.Properties;
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; }

    /// <summary>
    /// Gets the overwrite behavior for the target upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var overwrite = options.OverwriteMode;
    /// </code>
    /// </example>
    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;
}
