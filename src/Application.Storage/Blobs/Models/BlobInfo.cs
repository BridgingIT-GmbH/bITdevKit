// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes provider-neutral blob metadata and custom properties.
/// </summary>
/// <example>
/// <code>
/// var info = new BlobInfo
/// {
///     Key = new BlobKey("reports", "2026/06/report.pdf"),
///     Length = 1024,
///     ContentType = ContentType.PDF
/// };
/// </code>
/// </example>
public sealed class BlobInfo
{
    /// <summary>
    /// Gets the key that identifies the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = info.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the blob content length in bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = info.Length;
    /// </code>
    /// </example>
    public long Length { get; init; }

    /// <summary>
    /// Gets the provider-neutral content type when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = info.ContentType?.MimeType();
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the provider-neutral SHA-256 content hash when known.
    /// </summary>
    /// <example>
    /// <code>
    /// var hash = info.ContentHash;
    /// </code>
    /// </example>
    public string ContentHash { get; init; }

    /// <summary>
    /// Gets the provider-dependent entity tag when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = info.ETag;
    /// </code>
    /// </example>
    public string ETag { get; init; }

    /// <summary>
    /// Gets the provider creation timestamp when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var created = info.CreatedAt;
    /// </code>
    /// </example>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the provider last-modified timestamp when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var modified = info.LastModifiedAt;
    /// </code>
    /// </example>
    public DateTimeOffset? LastModifiedAt { get; init; }

    /// <summary>
    /// Gets the optional UTC expiration timestamp after which retention sweeping may delete the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var expiresAt = info.ExpiresAt;
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets custom application properties associated with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var customerId = info.Properties.Get&lt;string&gt;("customerId");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; } = new();
}
