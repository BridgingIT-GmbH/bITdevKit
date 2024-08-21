// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

public class Price : ValueObject // TODO: or use Money?
{
    private Price()
    {
    }

    private Price(decimal amount, string currency)
    {
        this.Amount = amount;
        this.Currency = currency;
    }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public static Price Create(decimal amount, string currency)
    {
        DomainRules.Apply(
        [
            PriceRules.ShouldBeInRange(amount),
        ]);

        return new Price(amount, currency);
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Amount;
        yield return this.Currency;
    }
}
