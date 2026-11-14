// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

/// <summary>Reports a fenced offline recovery boundary separately from cache-content retention and physical cleanup.</summary>
/// <param name="StoreId">The resulting store identity, or the fenced original identity when reset is required.</param>
/// <param name="RecoveryGeneration">The durable generation excluding earlier provider instances and continuations.</param>
/// <param name="Completed">Whether the root can be reopened by newly constructed providers.</param>
/// <param name="VerifiedShards">The committed shards verified without adopting older data or inventing payload metadata.</param>
/// <param name="DiscardedShards">The physical data-shard directories discarded by an explicit whole-store reset.</param>
/// <param name="RequiresReset">Whether damaged or uncertain committed metadata requires the separate explicit reset action.</param>
/// <param name="Reason">A controlled explanation without keys or payload content.</param>
/// <example><code>if (outcome.RequiresReset) { ScheduleAnExplicitReset(outcome.StoreId); }</code></example>
public sealed record CacheFileSystemRecoveryOutcome(string StoreId, long RecoveryGeneration, bool Completed,
    int VerifiedShards, int DiscardedShards, bool RequiresReset, string Reason = null);