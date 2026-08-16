// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Globalization;

/// <summary>
/// Represents money.
/// </summary>
public class Money : DecimalValueObject
{
    private int? cachedHashCode;

    private Money() { }

    private Money(decimal amount, Currency currency)
        : base(amount)
    {
        this.Currency = currency;
    }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public Currency Currency { get; protected set; }

    /// <summary>
    /// Executes the zero operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static Money Zero()
    {
        return Create(0);
    }

    /// <summary>
    /// Executes the zero operation.
    /// </summary>
    /// <param name="currency">The currency used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static Money Zero(Currency currency)
    {
        return Create(0, currency);
    }

    /// <summary>
    /// Determines whether is zero.
    /// </summary>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool IsZero()
    {
        return this.Amount == 0;
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    /// <summary>
    /// Executes the implicit operator decimal operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator decimal(Money value)
    {
        return value.Amount;
    }
#pragma warning restore SA1201 // Elements should appear in the correct order

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Money a, Money b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Executes the operator + operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new InvalidOperationException("Cannot calculate money with different currencies");
        }

        return new Money(a.Amount + b.Amount, a.Currency);
    }

    /// <summary>
    /// Executes the operator   operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new InvalidOperationException("Cannot calculate money with different currencies");
        }

        return new Money(a.Amount - b.Amount, a.Currency);
    }

    /// <summary>
    /// Creates .
    /// </summary>
    /// <param name="amount">The amount used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static Money Create(decimal amount)
    {
        return new Money(amount, Currency.UsDollar);
    }

    /// <summary>
    /// Creates .
    /// </summary>
    /// <param name="amount">The amount used by the operation.</param>
    /// <param name="currency">The currency used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static Money Create(decimal amount, Currency currency)
    {
        EnsureArg.IsNotNull(currency, nameof(currency));

        return new Money(amount, currency);
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (obj is null || obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((Money)obj);
    }

    /// <summary>
    ///     Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    ///     A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override int GetHashCode()
    {
        return this.cachedHashCode ??= this.GetAtomicValues()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Format(this.Amount, this.Currency.Code);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Currency.Code;
        yield return this.Amount;
    }

    /// <inheritdoc/>
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
