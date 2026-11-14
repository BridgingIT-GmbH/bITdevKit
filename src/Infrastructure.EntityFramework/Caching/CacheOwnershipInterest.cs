// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.Text.Json.Serialization;

/// <summary>Stores one bounded node observation group's interest inside its owning source flight's JSON state.</summary>
/// <param name="InterestId">The exact opaque observation-group identifier, bounded to 128 UTF-8 bytes.</param>
/// <param name="DeadlineTicks">The fixed UTC interest deadline, which never extends the source-flight deadline.</param>
/// <remarks>These children have no independent Entity Framework table or lifetime.</remarks>
/// <example><code>var interest = new CacheOwnershipInterest(groupId, deadline.UtcTicks);</code></example>
public sealed record CacheOwnershipInterest([property: JsonPropertyName("i")] string InterestId, [property: JsonPropertyName("d")] long DeadlineTicks);