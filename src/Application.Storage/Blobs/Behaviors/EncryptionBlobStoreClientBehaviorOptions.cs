// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures the blob-store encryption behavior.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithEncryptionBehavior(options => options.StoredContentType = ContentType.BIN);
/// </code>
/// </example>
public sealed class EncryptionBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the provider content type used for encrypted bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// options.StoredContentType = ContentType.BIN;
    /// </code>
    /// </example>
    public ContentType StoredContentType { get; set; } = ContentType.BIN;

}
