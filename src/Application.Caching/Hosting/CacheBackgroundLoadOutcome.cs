// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports background source/publication facts without retaining its typed value or a consumable stream.</summary>
/// <param name="StorageOutcome">The actual retention acknowledgement.</param>
/// <param name="Version">The confirmed published or acquired version, when available.</param>
/// <param name="IsNotFound">Whether the source or stored marker confirmed absence.</param>
/// <example><code>var published = outcome.StorageOutcome == CacheStorageOutcome.Stored;</code></example>
public sealed record CacheBackgroundLoadOutcome(CacheStorageOutcome StorageOutcome, CacheEntryVersion? Version, bool IsNotFound);

/// <summary>Invokes one code-registered typed or binary background loader through its ordinary decorated collection.</summary>
/// <param name="cache">The container-owned application cache.</param>
/// <param name="binding">The matching validated collection binding.</param>
/// <param name="item">The captured key and dimensions.</param>
/// <param name="refresh">Whether to conditionally refresh an existing selected version.</param>
/// <param name="version">The selected refresh version, or null for ordinary warm-up.</param>
/// <param name="cancellationToken">The background caller's wait token.</param>
/// <returns>The actual source/publication facts without payload ownership escaping the worker.</returns>
public delegate ValueTask<Result<CacheBackgroundLoadOutcome>> CacheBackgroundLoadDelegate(IApplicationCache cache,
    CacheCollectionBinding binding, CacheItem item, bool refresh, CacheEntryVersion? version, CancellationToken cancellationToken);