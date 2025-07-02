// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class LatitudeShouldBeInRangeRule : RuleBase
{
    private readonly double? value;

    public LatitudeShouldBeInRangeRule(double? value)
    {
        this.value = value;
    }

    public LatitudeShouldBeInRangeRule(double value)
    {
        this.value = value;
    }

    public override string Message => "Latitude should be between -90 and 90";

    public override Result Execute()
    {
        return Result.SuccessIf(this.value is null || (this.value >= -180 && this.value <= 180));
    }
}

public static partial class DinnerRules
{
    public static IRule LatitudeShouldBeInRange(double? value)
    {
        return new LatitudeShouldBeInRangeRule(value);
    }
}