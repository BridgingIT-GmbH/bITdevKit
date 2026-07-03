// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Describes the result kind of a business-day rule.
/// </summary>
public enum BusinessDayRuleResultKind
{
    /// <summary>The rule does not apply.</summary>
    NoMatch,

    /// <summary>The rule marks the date as working.</summary>
    WorkingDay,

    /// <summary>The rule marks the date as non-working.</summary>
    NonWorkingDay
}
