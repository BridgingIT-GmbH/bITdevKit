// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;

public class RatingShouldBeInRangeRule(int value) : IBusinessRule
{
    private readonly double? value = value;

    public string Message => "Rating should be between 1 and 5";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.value >= 1 && this.value <= 5);
    }
}

public static partial class RatingRules
{
    public static IBusinessRule ShouldBeInRange(int value) => new RatingShouldBeInRangeRule(value);
}