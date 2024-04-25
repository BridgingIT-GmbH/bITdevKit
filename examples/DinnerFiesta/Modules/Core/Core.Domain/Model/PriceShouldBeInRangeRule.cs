// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;

public class PriceShouldBeInRangeRule : IBusinessRule
{
    private readonly decimal amount;

    public PriceShouldBeInRangeRule(decimal amount)
    {
        this.amount = amount;
    }

    public string Message => "Price should be between 1 and 100";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.amount >= 1 && this.amount <= 100);
    }
}