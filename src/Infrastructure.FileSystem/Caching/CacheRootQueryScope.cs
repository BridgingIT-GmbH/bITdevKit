// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Captures a collection epoch and fixed maximum committed sequence for each of its 256 shards.</summary>
public sealed record CacheCollectionBoundary(long Epoch, IReadOnlyList<long> MaximumSequence);