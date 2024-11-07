// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Domain;

using BridgingIT.DevKit.Common;

public class LongitudeShouldBeInRange : RuleBase
{
    private readonly double? value;

    public LongitudeShouldBeInRange(double? value)
    {
        this.value = value;
    }

    public LongitudeShouldBeInRange(double value)
    {
        this.value = value;
    }

    public override string Message => "Longitude should be between -180 and 180";

    protected override Result Execute()
    {
        return Result.SuccessIf(this.value.HasValue && this.value >= -180 && this.value <= 180);
    }
}