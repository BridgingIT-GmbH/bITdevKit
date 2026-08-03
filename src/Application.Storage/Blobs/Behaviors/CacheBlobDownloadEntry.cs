// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a cached blob download payload used by <see cref="CacheBlobStoreClientBehavior" />.
/// </summary>
/// <example>
/// <code>
/// var entry = new CacheBlobDownloadEntry { Info = info, Content = bytes };
/// </code>
/// </example>
public sealed class CacheBlobDownloadEntry
{
    /// <summary>
    /// Gets the cached blob metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = entry.Info.Key;
    /// </code>
    /// </example>
    public BlobInfo Info { get; init; }

    /// <summary>
    /// Gets the cached blob content bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = entry.Content.Length;
    /// </code>
    /// </example>
    public byte[] Content { get; init; } = [];
}
