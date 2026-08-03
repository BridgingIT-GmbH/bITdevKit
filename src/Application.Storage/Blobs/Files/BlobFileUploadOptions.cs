// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures a file-to-blob transfer.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobFileUploadOptions
/// {
///     ContentType = ContentType.PDF,
///     OverwriteMode = BlobOverwriteMode.FailIfExists
/// };
/// </code>
/// </example>
public sealed class BlobFileUploadOptions
{
    /// <summary>
    /// Gets the explicit content type to store with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var contentType = options.ContentType;
    /// </code>
    /// </example>
    public ContentType? ContentType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the content type should be inferred from the source file name when no explicit
    /// content type is supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var infer = options.InferContentTypeFromFileName;
    /// </code>
    /// </example>
    public bool InferContentTypeFromFileName { get; init; } = true;

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
    /// Gets the custom application properties to store with the blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var source = options.Properties.Get&lt;string&gt;("source");
    /// </code>
    /// </example>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>
    /// Gets the overwrite behavior for the blob upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var overwriteMode = options.OverwriteMode;
    /// </code>
    /// </example>
    public BlobOverwriteMode OverwriteMode { get; init; } = BlobOverwriteMode.Overwrite;
}
