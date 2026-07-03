// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies relative-time units.
/// </summary>
public enum RelativeTimeUnit
{
    /// <summary>Milliseconds.</summary>
    Millisecond,

    /// <summary>Seconds.</summary>
    Second,

    /// <summary>Minutes.</summary>
    Minute,

    /// <summary>Hours.</summary>
    Hour,

    /// <summary>Days.</summary>
    Day,

    /// <summary>Weeks.</summary>
    Week,

    /// <summary>Approximate months of 30 days.</summary>
    Month,

    /// <summary>Approximate years of 365 days.</summary>
    Year
}
