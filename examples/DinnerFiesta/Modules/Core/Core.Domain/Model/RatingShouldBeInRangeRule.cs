﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using DevKit.Domain;

public class RatingShouldBeInRangeRule(int value) : IDomainRule
{
    private readonly double? value = value;

    public string Message => "Rating should be between 1 and 5";

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.value >= 1 && this.value <= 5);
    }
}

public static class RatingRules
{
    public static IDomainRule ShouldBeInRange(int value)
    {
        return new RatingShouldBeInRangeRule(value);
    }
}