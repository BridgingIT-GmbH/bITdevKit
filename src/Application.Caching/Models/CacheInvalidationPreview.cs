// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes a bounded metadata-only invalidation preview without claiming a frozen future result.</summary>
/// <example><code>var value = new CacheInvalidationPreview();</code></example>
public sealed record CacheInvalidationPreview
{
    /// <summary>Gets the observed candidate count.</summary>
    public long Count { get; init; }

    /// <summary>Gets whether the observed count covers the entire declared scope.</summary>
    public bool CountIsExact { get; init; }

    /// <summary>Gets a bounded metadata sample; no payload is opened.</summary>
    public IReadOnlyList<CacheEntryMetadata> Sample { get; init; } = [];

    /// <summary>Gets a continuation when bounded inspection is incomplete.</summary>
    public string Continuation { get; init; }

}