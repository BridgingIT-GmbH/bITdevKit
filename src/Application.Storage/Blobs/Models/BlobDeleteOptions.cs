// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures an exact blob delete operation.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobDeleteOptions { IfMatchETag = info.ETag };
/// </code>
/// </example>
public sealed class BlobDeleteOptions
{
    /// <summary>
    /// Gets the optional entity tag that must match the current blob.
    /// </summary>
    /// <example>
    /// <code>
    /// var expected = options.IfMatchETag;
    /// </code>
    /// </example>
    public string IfMatchETag { get; init; }
}
