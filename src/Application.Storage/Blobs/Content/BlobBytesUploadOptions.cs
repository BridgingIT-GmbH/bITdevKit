// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures a byte-array blob upload.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobBytesUploadOptions
/// {
///     ContentType = ContentType.BIN
/// };
/// </code>
/// </example>
public sealed class BlobBytesUploadOptions
{
    /// <summary>
    /// Gets the content type to store with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var contentType = options.ContentType;
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; } = BridgingIT.DevKit.Common.ContentType.BIN;

    /// <summary>
    /// Gets the expected SHA-256 blob content hash when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var expectedHash = options.ExpectedContentHash;
    /// </code>
    /// </example>
    public string ExpectedContentHash { get; init; }

    /// <summary>
    /// Gets custom application properties to store with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var source = options.Properties.Get&lt;string&gt;("source");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the overwrite behavior for the upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var overwriteMode = options.OverwriteMode;
    /// </code>
    /// </example>
    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;
}
