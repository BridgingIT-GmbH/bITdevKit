// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;

/// <summary>
/// Configures a text-to-blob upload.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobTextUploadOptions
/// {
///     ContentType = ContentType.JSON,
///     OverwriteMode = BlobOverwriteMode.FailIfExists
/// };
/// </code>
/// </example>
public sealed class BlobTextUploadOptions
{
    /// <summary>
    /// Gets the text encoding used to convert the string to bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var encoding = options.Encoding;
    /// </code>
    /// </example>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// Gets the content type to store with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var contentType = options.ContentType;
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; } = BridgingIT.DevKit.Common.ContentType.TXT;

    /// <summary>
    /// Gets a value indicating whether binary content types are rejected for text uploads.
    /// </summary>
    /// <example>
    /// <code>
    /// var rejectBinary = options.RejectBinaryContentType;
    /// </code>
    /// </example>
    public bool RejectBinaryContentType { get; init; } = true;

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
    /// Gets the overwrite behavior for the text upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var overwriteMode = options.OverwriteMode;
    /// </code>
    /// </example>
    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;
}
