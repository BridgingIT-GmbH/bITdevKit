// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Rule that checks whether the user's subscription plan allows city comparison.
/// </summary>
public class SubscriptionComparisonAllowedRule(UserSubscription subscription) : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    public override string Message =>
        $"Subscription plan '{subscription.Plan.Value}' does not allow city comparison.";

    /// <summary>
    /// Executes the validation rule.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
    public override Result Execute() =>
        Result.SuccessIf(subscription.Plan.Details.AllowsComparison, new DomainPolicyError([this.Message]));
}
