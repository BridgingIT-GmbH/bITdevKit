// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Binds a proposed invalidation checkpoint to the exact operation revision, continuation and node claim it may advance.</summary>
public sealed record CacheNativeTrackedProgress(string OperationId, string ExpectedRevision, string ExpectedContinuation,
    CacheInvalidationOutcome Outcome, string NodeId, string Revision);