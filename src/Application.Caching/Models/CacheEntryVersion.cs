// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies an acquired entry snapshot; callers must not interpret or synthesize concurrency semantics.</summary>
/// <param name="Value">The opaque provider-owned token.</param>
/// <example><code>var expected = lookup.Version.Value;</code></example>
public readonly record struct CacheEntryVersion(string Value)
{
    /// <summary>Creates a collision-resistant version for a newly published entry.</summary>
    public static CacheEntryVersion New() => new(Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public override string ToString() => this.Value;
}