// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Evaluates whether a date should override default business-day behavior.
/// </summary>
/// <remarks><example><code>var result = rule.Evaluate(date);</code></example></remarks>
public interface IBusinessDayRule
{
    /// <summary>Evaluates a date.</summary>
    /// <param name="date">The date to evaluate.</param>
    /// <returns>The rule result.</returns>
    /// <remarks><example><code>var result = rule.Evaluate(date);</code></example></remarks>
    BusinessDayRuleResult Evaluate(DateOnly date);
}
