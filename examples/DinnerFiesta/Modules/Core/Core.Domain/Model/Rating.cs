// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class Rating : ValueObject
{
    private Rating() { }

    private Rating(int value)
    {
        this.Value = value;
    }

    public int Value { get; }

    public static Rating Create(int value)
    {
        return Rule.Add(
                RatingRules.ShouldBeInRange(value)).Check()
            .ThrowIfFailed()
            .Map(new Rating(value)).Value;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}