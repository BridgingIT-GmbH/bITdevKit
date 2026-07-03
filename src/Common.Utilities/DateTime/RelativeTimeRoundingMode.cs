// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies how relative-time values are rounded.
/// </summary>
public enum RelativeTimeRoundingMode
{
    /// <summary>Floor the value.</summary>
    Floor,

    /// <summary>Round the value to the nearest whole number.</summary>
    Round,

    /// <summary>Ceiling the value.</summary>
    Ceiling
}
