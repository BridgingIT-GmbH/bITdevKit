// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Exposes one bounded range-scan key, sequence, tombstone flag and record view valid only during its current enumeration step.</summary>
public sealed record CacheShardScanValue(CacheIndexKey Key, long Sequence, bool Deleted, ReadOnlyMemory<byte> Bytes);