// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>States how an observation was established.</summary>
/// <example><code>var category = CacheObservationAccuracy.Exact;</code></example>
public enum CacheObservationAccuracy
{
    /// <summary>A directly accounted process or provider observation.</summary>
    Exact,

    /// <summary>A conservative explicitly identified estimate.</summary>
    Estimated,

    /// <summary>A bounded subset or sampled time-series observation.</summary>
    Sampled,

    /// <summary>No observation supports a numeric value.</summary>
    Unavailable
}