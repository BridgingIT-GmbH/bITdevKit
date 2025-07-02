// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class PriceShouldBeInRangeRule(decimal amount) : RuleBase
{
    private readonly decimal amount = amount;

    public override string Message => "Price should be between 1 and 100";

    public override Result Execute()
    {
        return Result.SuccessIf(this.amount >= 1 && this.amount <= 100);
    }
}

public static class PriceRules
{
    public static IRule ShouldBeInRange(decimal amount)
    {
        return new PriceShouldBeInRangeRule(amount);
    }
}