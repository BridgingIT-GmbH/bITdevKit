// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents downloaded text content and its blob metadata.
/// </summary>
/// <example>
/// <code>
/// var text = result.Value.Text;
/// var hash = result.Value.Info.ContentHash;
/// </code>
/// </example>
public sealed class BlobTextContent
{
    /// <summary>
    /// Gets the blob information returned with the text content.
    /// </summary>
    /// <example>
    /// <code>
    /// var info = content.Info;
    /// </code>
    /// </example>
    public BlobInfo Info { get; init; }

    /// <summary>
    /// Gets the downloaded text.
    /// </summary>
    /// <example>
    /// <code>
    /// var text = content.Text;
    /// </code>
    /// </example>
    public string Text { get; init; }
}
