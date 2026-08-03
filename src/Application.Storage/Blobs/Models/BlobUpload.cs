// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a stream-first blob upload request.
/// </summary>
/// <example>
/// <code>
/// var upload = new BlobUpload
/// {
///     Key = new BlobKey("reports", "2026/06/report.pdf"),
///     Content = sourceStream,
///     ContentType = ContentType.PDF
/// };
/// </code>
/// </example>
public sealed class BlobUpload
{
    /// <summary>
    /// Gets the destination blob key.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = upload.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the readable upload stream owned by the caller.
    /// </summary>
    /// <example>
    /// <code>
    /// var canRead = upload.Content.CanRead;
    /// </code>
    /// </example>
    public Stream Content { get; init; }

    /// <summary>
    /// Gets the provider-neutral content type when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var mimeType = upload.ContentType?.MimeType();
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets the expected SHA-256 content hash when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var expectedHash = upload.ExpectedContentHash;
    /// </code>
    /// </example>
    public string ExpectedContentHash { get; init; }

    /// <summary>
    /// Gets the optional UTC expiration timestamp after which retention sweeping may delete the blob.
    /// Providers normalize non-UTC offsets to UTC before storing metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var expiresAt = upload.ExpiresAt;
    /// </code>
    /// </example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets custom application properties to store with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var source = upload.Properties.Get&lt;string&gt;("source");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the overwrite behavior for the upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var overwriteMode = upload.OverwriteMode;
    /// </code>
    /// </example>
    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;
}
