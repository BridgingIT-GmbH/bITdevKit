// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports actual publication acknowledgement without collapsing rejection and uncertainty into success.</summary>
/// <param name="StorageOutcome">The provider's acknowledgement class.</param>
/// <param name="Version">The confirmed acquired version, absent unless stored.</param>
/// <param name="Metadata">Confirmed version facts when publication succeeded.</param>
/// <example><code>if (outcome.StorageOutcome == CacheStorageOutcome.Stored) { Use(outcome.Version); }</code></example>
public readonly record struct CacheWriteOutcome(
    CacheStorageOutcome StorageOutcome,
    CacheEntryVersion? Version = null,
    CacheEntryMetadata Metadata = null);

/// <summary>Conditions a mutation atomically at its provider commit boundary.</summary>
/// <param name="ExpectedVersion">An acquired version that must still be current.</param>
/// <param name="CreateOnly">Whether no valid positive entry may exist. Confirmed absence is eligible for creation; a positive null or empty value is present.</param>
/// <example><code>var condition = new CacheWriteCondition(lookup.Version);</code></example>
public readonly record struct CacheWriteCondition(CacheEntryVersion? ExpectedVersion = null, bool CreateOnly = false);