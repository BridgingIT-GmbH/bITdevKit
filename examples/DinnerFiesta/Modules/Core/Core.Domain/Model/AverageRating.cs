// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;

public class AverageRating : ValueObject
{
    private double value;

    private AverageRating() { }

    private AverageRating(double value, int numRatings)
    {
        this.Value = value;
        this.NumRatings = numRatings;
    }

    public double? Value
    {
        get => this.NumRatings > 0 ? this.value : null;
        private set => this.value = value!.Value;
    }

    public int NumRatings { get; private set; }

    public static implicit operator double(AverageRating rating)
    {
        return rating.Value ?? 0;
    }

    public static AverageRating Create(double value = 0, int numRatings = 0)
    {
        return new AverageRating(value, numRatings);
    }

    public void Add(Rating rating)
    {
        this.Value = ((this.value * this.NumRatings) + rating.Value) / ++this.NumRatings;
    }

    public void Remove(Rating rating)
    {
        if (this.NumRatings == 0)
        {
            return;
        }

        this.Value = ((this.Value * this.NumRatings) - rating.Value) / --this.NumRatings;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Value;
    }
}