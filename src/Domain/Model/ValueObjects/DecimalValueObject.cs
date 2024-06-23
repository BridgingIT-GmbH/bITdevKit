// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public abstract class DecimalValueObject : ComparableValueObject
{
    private int? cachedHashCode;

    protected DecimalValueObject() // TODO: make private again when System.Text.Json can deserialize objects with a non-public ctor
    {
    }

    protected DecimalValueObject(decimal value)
    {
        this.Amount = value;
    }

    //public static DecimalValueObject Zero => new(0);

    public decimal Amount { get; protected set; }

    //public static implicit operator DecimalValueObject(decimal value) => new(value);

    public static implicit operator decimal(DecimalValueObject value) => value.Amount;

    public static bool operator ==(DecimalValueObject a, DecimalValueObject b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is not null && b is not null)
        {
            return a.Amount.Equals(b.Amount);
        }

        return false;
    }

    public static bool operator !=(DecimalValueObject a, DecimalValueObject b) => !(a == b);

    public static bool operator >(DecimalValueObject a, DecimalValueObject b) => a.Amount > b.Amount;

    public static bool operator <(DecimalValueObject a, DecimalValueObject b) => a.Amount < b.Amount;

    public static bool operator >=(DecimalValueObject a, DecimalValueObject b) => a.Amount >= b.Amount;

    public static bool operator <=(DecimalValueObject a, DecimalValueObject b) => a.Amount <= b.Amount;

    //public static DecimalValueObject operator +(DecimalValueObject a, DecimalValueObject b) => a.Value + b.Value;

    //public static DecimalValueObject operator -(DecimalValueObject a, DecimalValueObject b) => a.Value - b.Value;

    //public static DecimalValueObject Create(decimal value) => new DecimalValueObject(value);

    public virtual bool HasDecimals()
    {
        return this.Amount - decimal.Truncate(this.Amount) != decimal.Zero;
    }

    public override bool Equals(object obj)
    {
        if (obj is null || obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((ValueObject)obj);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override int GetHashCode()
    {
        return this.cachedHashCode ??= this.GetAtomicValues()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public override string ToString() => this.Amount.ToString("0.00", CultureInfo.InvariantCulture);

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Amount;
    }

    protected override IEnumerable<IComparable> GetComparableAtomicValues()
    {
        yield return this.Amount;
    }
}