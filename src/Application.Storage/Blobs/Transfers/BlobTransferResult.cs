// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a completed blob transfer operation.
/// </summary>
/// <example>
/// <code>
/// var target = result.Value.Target;
/// </code>
/// </example>
public sealed class BlobTransferResult
{
    /// <summary>
    /// Gets the source blob metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var source = result.Source;
    /// </code>
    /// </example>
    public BlobInfo Source { get; init; }

    /// <summary>
    /// Gets the target blob metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var target = result.Target;
    /// </code>
    /// </example>
    public BlobInfo Target { get; init; }

    /// <summary>
    /// Gets a value indicating whether the source blob was deleted.
    /// </summary>
    /// <example>
    /// <code>
    /// var deleted = result.SourceDeleted;
    /// </code>
    /// </example>
    public bool SourceDeleted { get; init; }
}
