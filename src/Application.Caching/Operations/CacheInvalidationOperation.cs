// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Requests explicit filter invalidation or clear for a bounded set of declared collection targets.</summary>
/// <param name="Collections">At most 32 distinct declared collection names; each target owns its own provider record.</param>
/// <param name="Selection">The validated filter. An empty filter matches nothing and never means clear.</param>
/// <param name="Clear">Whether this is an explicit clear operation across retained contract versions.</param>
/// <param name="Background">Whether to accept provider status before maintenance executes any mutation.</param>
/// <param name="Origin">The bounded management adapter origin.</param>
/// <example><code>var request = new CacheInvalidationOperation(["cards"], CacheSelection.Prefix("region:"), Background: true);</code></example>
public sealed record CacheInvalidationOperation(IReadOnlyList<string> Collections, CacheSelection Selection,
    bool Clear = false, bool Background = false, string Origin = null);

/// <summary>Identifies a child operation in one selected complete provider and logical collection.</summary>
/// <param name="Provider">The declared provider alias.</param>
/// <param name="Collection">The declared logical collection.</param>
/// <param name="Id">The opaque provider-owned operation identifier.</param>
/// <example><code>var status = await operations.GetStatusAsync(handle, cancellationToken);</code></example>
public sealed record CacheOperationHandle(string Provider, string Collection, string Id);

/// <summary>Reports one target's actual acceptance or bounded synchronous progress.</summary>
/// <param name="Handle">The opaque child handle for later status and controls.</param>
/// <param name="Status">The provider's confirmed status and checkpoint.</param>
/// <param name="Accepted">Whether background work was accepted before executing a mutation.</param>
/// <example><code>var completed = submission.Status.State == CacheOperationState.Completed;</code></example>
public sealed record CacheOperationSubmission(CacheOperationHandle Handle, CacheProviderOperation Status, bool Accepted);