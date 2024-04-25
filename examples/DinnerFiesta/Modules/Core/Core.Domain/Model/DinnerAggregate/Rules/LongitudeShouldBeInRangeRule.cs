// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;

public class LongitudeShouldBeInRangeRule : IBusinessRule
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

    public string Message => "Longitude should be between -180 and 180";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.value is null || (this.value >= -180 && this.value <= 180));
    }
}