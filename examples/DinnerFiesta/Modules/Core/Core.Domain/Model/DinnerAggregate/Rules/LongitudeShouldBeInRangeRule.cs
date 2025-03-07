// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class LongitudeShouldBeInRangeRule : RuleBase
{
    private readonly double? value;

    public LongitudeShouldBeInRangeRule(double? value)
    {
        this.value = value;
    }

    public LongitudeShouldBeInRangeRule(double value)
    {
        this.value = value;
    }

    public override string Message => "Longitude should be between -180 and 180";

    public override Result Execute()
    {
        return Result.SuccessIf(this.value is null || (this.value >= -180 && this.value <= 180));
    }
}

public static partial class DinnerRules
{
    public static IRule LongitudeShouldBeInRange(double? value)
    {
        return new LongitudeShouldBeInRangeRule(value);
    }
}