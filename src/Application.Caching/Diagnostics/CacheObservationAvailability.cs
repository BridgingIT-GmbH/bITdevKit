// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Distinguishes meaningful measurements from missing instrumentation.</summary>
/// <example><code>var category = CacheObservationAvailability.Available;</code></example>
public enum CacheObservationAvailability
{
    /// <summary>The observation source is active.</summary>
    Available,

    /// <summary>The host disabled the observation source.</summary>
    Disabled,

    /// <summary>The source has no retained measurements for this family or window.</summary>
    NoSamples,

    /// <summary>Optional observation storage could not be admitted.</summary>
    Capacity,

    /// <summary>The underlying source or query is unavailable.</summary>
    Unavailable
}