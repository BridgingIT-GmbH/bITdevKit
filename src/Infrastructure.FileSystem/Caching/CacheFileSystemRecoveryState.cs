// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

/// <summary>Exposes the checked root identity needed for an explicit offline maintenance acknowledgement.</summary>
/// <param name="StoreId">The exact current root identity.</param>
/// <param name="RecoveryGeneration">The persisted recovery fence.</param>
/// <param name="RecoveryInProgress">Whether ordinary providers must remain unavailable.</param>
/// <param name="AccessKind">The declared native access profile.</param>
/// <param name="Durability">The declared acknowledgement profile.</param>
/// <example><code>var state = await recovery.GetStateAsync(cancellationToken);</code></example>
public sealed record CacheFileSystemRecoveryState(string StoreId, long RecoveryGeneration, bool RecoveryInProgress, string AccessKind, string Durability);