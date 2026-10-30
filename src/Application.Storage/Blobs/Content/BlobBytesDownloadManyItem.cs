// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents the outcome of downloading a single blob as part of a bulk byte-array download.
/// </summary>
/// <example>
/// <code>
/// foreach (var item in result.Value)
/// {
///     if (item.Result.IsSuccess)
///     {
///         var bytes = item.Result.Value.Bytes;
///     }
/// }
/// </code>
/// </example>
public sealed class BlobBytesDownloadManyItem
{
    /// <summary>
    /// Gets the requested blob key.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = item.Key;
    /// </code>
    /// </example>
    public BlobKey Key { get; init; }

    /// <summary>
    /// Gets the download result for the key, including the bytes and blob metadata on success.
    /// </summary>
    /// <example>
    /// <code>
    /// var isNotFound = item.Result.HasError&lt;BlobStoreNotFoundError&gt;();
    /// </code>
    /// </example>
    public Result<BlobBytesContent> Result { get; init; }
}
