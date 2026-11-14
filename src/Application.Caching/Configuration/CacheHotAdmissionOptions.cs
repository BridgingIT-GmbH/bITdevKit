// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Bounds second-access hot admission observations.</summary>
/// <example><code>var options = new CacheHotAdmissionOptions();</code></example>
public sealed record CacheHotAdmissionOptions
{
    /// <summary>Gets successful accesses required before retention.</summary>
    public int MinimumAccesses { get; init; } = 2;

    /// <summary>Gets observation expiry window.</summary>
    public TimeSpan ObservationWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets hard admission tracker count.</summary>
    public int MaxTrackingEntries { get; init; } = 20000;
}