// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Carries authenticated query progress, fixed cutoff and per-shard sequence bounds; serialize only through the provider&apos;s encrypted continuation contract.</summary>
public sealed record CacheFileQueryCursor(int Format, string StoreId, long RecoveryGeneration, string Scope, string SelectionHash, long CutoffTicks, string Purpose,
    int Shard, string LastKey, long Epoch, IReadOnlyList<long> MaximumSequence, long Observed, int Examined, int MaximumCandidates, bool Changed);