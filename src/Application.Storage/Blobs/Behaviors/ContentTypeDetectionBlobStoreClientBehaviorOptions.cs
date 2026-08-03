// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures blob content-type detection from blob name extensions.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithContentTypeDetectionBehavior(options => options.DefaultContentType = ContentType.BIN);
/// </code>
/// </example>
public sealed class ContentTypeDetectionBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the fallback content type used when a blob name has an extension that is not known.
    /// </summary>
    /// <example>
    /// <code>
    /// options.DefaultContentType = ContentType.BIN;
    /// </code>
    /// </example>
    public ContentType DefaultContentType { get; set; } = ContentType.DEFAULT;
}
