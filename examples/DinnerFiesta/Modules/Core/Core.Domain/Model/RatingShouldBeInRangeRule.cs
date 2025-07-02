// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class RatingShouldBeInRangeRule(int value) : RuleBase
{
    private readonly double? value = value;

    public override string Message => "Rating should be between 1 and 5";

    public override Result Execute()
    {
        return Result.SuccessIf(this.value >= 1 && this.value <= 5);
    }
}

public static class RatingRules
{
    public static IRule ShouldBeInRange(int value)
    {
        return new RatingShouldBeInRangeRule(value);
    }
}