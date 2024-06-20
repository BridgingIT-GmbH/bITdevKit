// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Globalization;
using System.Linq;

public class Money : DecimalValueObject
{
    private int? cachedHashCode;

    private Money()
    {
    }

    private Money(decimal amount, Currency currency)
        : base(amount)
    {
        this.Currency = currency;
    }

    public Currency Currency { get; }

    public static Money Zero() => For(0);

    public static Money Zero(Currency currency) => For(0, currency);

    public bool IsZero() => this.Amount == 0;

#pragma warning disable SA1201 // Elements should appear in the correct order
    public static implicit operator decimal(Money value) => value.Amount;
    //public static implicit operator Money(decimal amount) => new(amount, currency);
#pragma warning restore SA1201 // Elements should appear in the correct order

    public static bool operator ==(Money a, Money b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is not null && b is not null)
        {
            return a.Amount.Equals(b.Amount) && a.Currency.Equals(b.Currency);
        }

        return false;
    }

    public static bool operator !=(Money a, Money b) => !(a == b);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new InvalidOperationException("Cannot calculate money with different currencies");
        }

        return new(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new InvalidOperationException("Cannot calculate money with different currencies");
        }

        return new(a.Amount - b.Amount, a.Currency);
    }

    public static Money For(decimal amount)
    {
        return new Money(amount, Currency.USDollar);
    }

    public static Money For(decimal amount, Currency currency)
    {
        EnsureArg.IsNotNull(currency, nameof(currency));

        return new Money(amount, currency);
    }

    public override bool Equals(object obj)
    {
        if (obj is null || obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((Money)obj);
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

    public override string ToString() => this.Format(this.Amount, this.Currency.Code);

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Currency.Code;
        yield return this.Amount;
    }

    protected override IEnumerable<IComparable> GetComparableAtomicValues()
    {
        yield return this.Currency.Code;
        yield return this.Amount;
    }

    private string Format(decimal amount, string currencyCode)
    {
        EnsureArg.IsNotNullOrEmpty(currencyCode, nameof(currencyCode));

        var culture = (from c in CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                       let r = this.CreateRegionInfo(c.Name)
                       where r is not null && string.Equals(r.ISOCurrencySymbol, currencyCode, StringComparison.OrdinalIgnoreCase)
                       select c).FirstOrDefault();

        if (culture is null)
        {
            return amount.ToString("0.00");
        }

        return string.Format(culture, "{0:C}", amount);
    }

    private RegionInfo CreateRegionInfo(string cultureName)
    {
        RegionInfo region;

        try
        {
            region = new RegionInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return default;
        }

        return region;
    }
}