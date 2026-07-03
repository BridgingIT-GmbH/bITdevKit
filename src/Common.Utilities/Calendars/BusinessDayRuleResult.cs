// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a business-day rule result.
/// </summary>
/// <param name="Kind">The result kind.</param>
/// <param name="Reason">Optional diagnostic reason.</param>
/// <remarks><example><code>return new BusinessDayRuleResult(BusinessDayRuleResultKind.NonWorkingDay, "Closure");</code></example></remarks>
public sealed record BusinessDayRuleResult(BusinessDayRuleResultKind Kind, string Reason = null)
{
    /// <summary>Gets a no-match result.</summary>
    public static BusinessDayRuleResult NoMatch { get; } = new(BusinessDayRuleResultKind.NoMatch);
}
