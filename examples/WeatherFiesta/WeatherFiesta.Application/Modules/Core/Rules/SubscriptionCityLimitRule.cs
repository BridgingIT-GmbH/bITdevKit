// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Rule that checks whether the user can add another city based on their subscription plan limits.
/// </summary>
public class SubscriptionCityLimitRule(UserSubscription subscription, long activeCityCount) : RuleBase
{
    /// <summary>
    /// Gets the message associated with the rule.
    /// </summary>
    public override string Message =>
        $"Subscription plan '{subscription.Plan.Value}' allows up to {subscription.Plan.Details.MaxCities} cities.";

    /// <summary>
    /// Executes the validation rule.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
    public override Result Execute()
    {
        var max = subscription.Plan.Details.MaxCities;
        return Result.SuccessIf(max < 0 || activeCityCount < max, new DomainPolicyError([this.Message]));
    }
}
