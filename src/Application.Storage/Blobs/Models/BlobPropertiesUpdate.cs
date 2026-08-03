// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a blob property update that does not upload content.
/// </summary>
/// <example>
/// <code>
/// var update = new BlobPropertiesUpdate
/// {
///     Key = new BlobKey("reports", "2026/06/report.pdf"),
///     ContentType = ContentType.PDF
/// };
/// </code>
/// </example>
public sealed class BlobPropertiesUpdate
{
    /// <summary>
    /// Gets the blob key to update.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = update.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the replacement content type when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = update.ContentType?.MimeType();
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the replacement UTC expiration timestamp. A null value clears any existing expiration.
    /// Providers normalize non-UTC offsets to UTC before storing metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// update.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the replacement custom property bag.
    /// </summary>
    /// <example>
    /// <code>
    /// var reviewed = update.Properties.Get&lt;bool&gt;("reviewed");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the optional entity tag required for optimistic property updates.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = update.IfMatchETag;
    /// </code>
    /// </example>
    public string IfMatchETag { get; init; }
}
