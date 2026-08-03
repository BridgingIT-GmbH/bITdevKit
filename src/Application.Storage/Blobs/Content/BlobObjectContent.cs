// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a deserialized blob object and its blob metadata.
/// </summary>
/// <typeparam name="T">The deserialized object type.</typeparam>
/// <example>
/// <code>
/// var value = result.Value.Value;
/// var info = result.Value.Info;
/// </code>
/// </example>
public sealed class BlobObjectContent<T>
{
    /// <summary>
    /// Gets the blob information returned with the serialized content.
    /// </summary>
    /// <example>
    /// <code>
    /// var info = content.Info;
    /// </code>
    /// </example>
    public BlobInfo Info { get; init; }

    /// <summary>
    /// Gets the deserialized value.
    /// </summary>
    /// <example>
    /// <code>
    /// var value = content.Value;
    /// </code>
    /// </example>
    public T Value { get; init; }
}
