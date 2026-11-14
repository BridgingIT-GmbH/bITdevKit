// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports logical invalidation progress, separately from physical purge.</summary>
/// <example><code>var value = new CacheInvalidationOutcome();</code></example>
public sealed record CacheInvalidationOutcome
{
    /// <summary>Gets whether the acknowledged visibility boundary covers the entire declared scope.</summary>
    public bool Completed { get; init; }

    /// <summary>Gets confirmed affected current entry versions.</summary>
    public long AffectedCount { get; init; }

    /// <summary>Gets whether the count is exact within the declared scope.</summary>
    public bool CountIsExact { get; init; } = true;

    /// <summary>Gets an opaque bounded-operation continuation when incomplete.</summary>
    public string Continuation { get; init; }

    /// <summary>Gets a provider-owned tracked operation identifier when requested.</summary>
    public string OperationId { get; init; }

    /// <summary>Gets explicitly failed child scopes rather than hiding partial effects.</summary>
    public IReadOnlyList<string> FailedCollections { get; init; } = [];
}