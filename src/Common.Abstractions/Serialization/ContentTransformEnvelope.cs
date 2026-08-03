// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes logical and stored content after an ordered transform pipeline.
/// </summary>
/// <example>
/// <code>
/// var envelope = new ContentTransformEnvelope { LogicalLength = 42 };
/// </code>
/// </example>
public sealed record ContentTransformEnvelope
{
    /// <summary>Gets the envelope version.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the logical untransformed byte length.</summary>
    public long LogicalLength { get; init; }

    /// <summary>Gets the logical canonical content hash.</summary>
    public string LogicalContentHash { get; init; }

    /// <summary>Gets the stored byte length.</summary>
    public long StoredLength { get; init; }

    /// <summary>Gets the stored canonical content hash.</summary>
    public string StoredContentHash { get; init; }

    /// <summary>Gets the ordered transform descriptors.</summary>
    public IReadOnlyList<ContentTransformDescriptor> Transforms { get; init; } = [];
}

/// <summary>Describes one persisted content transform.</summary>
/// <example><code>var transform = new ContentTransformDescriptor { Id = "gzip" };</code></example>
public sealed record ContentTransformDescriptor
{
    /// <summary>Gets the stable transform identifier.</summary>
    public string Id { get; init; }

    /// <summary>Gets the transform metadata.</summary>
    public PropertyBag Properties { get; init; } = new();
}
