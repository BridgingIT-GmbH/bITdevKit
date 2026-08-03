// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Stores one in-memory blob entry.
/// </summary>
/// <example>
/// <code>
/// var entry = new InMemoryBlobEntry { Content = bytes, Info = info };
/// </code>
/// </example>
public sealed class InMemoryBlobEntry
{
    /// <summary>
    /// Gets or initializes the stored blob content bytes.
    /// </summary>
    /// <example>
    /// <code>
    /// var length = entry.Content.Length;
    /// </code>
    /// </example>
    public byte[] Content { get; init; }

    /// <summary>
    /// Gets or initializes the stored blob metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var key = entry.Info.Key;
    /// </code>
    /// </example>
    public BlobInfo Info { get; init; }
}
