// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Globalization;

/// <summary>
/// Represents decimal value object.
/// </summary>
public abstract class DecimalValueObject : ComparableValueObject
{
    private int? cachedHashCode;

    /// <summary>
    /// Initializes a new instance of the <c>DecimalValueObject</c> class.
    /// </summary>
    protected
        DecimalValueObject() // TODO: make private again when System.Text.Json can deserialize objects with a non-public ctor
    { }

    /// <summary>
    /// Initializes a new instance of the <c>DecimalValueObject</c> class.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    protected DecimalValueObject(decimal value)
    {
        this.Amount = value;
    }

    //public static DecimalValueObject Zero => new(0);

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; protected set; }

    //public static implicit operator DecimalValueObject(decimal value) => new(value);

    /// <summary>
    /// Executes the implicit operator decimal operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator decimal(DecimalValueObject value)
    {
        return value.Amount;
    }

    /// <summary>
    /// Executes the operator == operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Executes the operator != operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(DecimalValueObject a, DecimalValueObject b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Executes the operator > operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator >(DecimalValueObject a, DecimalValueObject b)
    {
        return a.Amount > b.Amount;
    }

    /// <summary>
    /// Executes the operator  operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator <(DecimalValueObject a, DecimalValueObject b)
    {
        return a.Amount < b.Amount;
    }

    /// <summary>
    /// Executes the operator >= operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator >=(DecimalValueObject a, DecimalValueObject b)
    {
        return a.Amount >= b.Amount;
    }

    /// <summary>
    /// Executes the operator  operation.
    /// </summary>
    /// <param name="a">The a used by the operation.</param>
    /// <param name="b">The b used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool operator <=(DecimalValueObject a, DecimalValueObject b)
    {
        return a.Amount <= b.Amount;
    }

    //public static DecimalValueObject operator +(DecimalValueObject a, DecimalValueObject b) => a.Value + b.Value;

    //public static DecimalValueObject operator -(DecimalValueObject a, DecimalValueObject b) => a.Value - b.Value;

    //public static DecimalValueObject Create(decimal value) => new DecimalValueObject(value);

    /// <summary>
    /// Determines whether has decimals.
    /// </summary>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public virtual bool HasDecimals()
    {
        return this.Amount - decimal.Truncate(this.Amount) != decimal.Zero;
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (obj is null || obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((ValueObject)obj);
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
        return this.Amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Amount;
    }

    /// <inheritdoc/>
    protected override IEnumerable<IComparable> GetComparableAtomicValues()
    {
        yield return this.Amount;
    }
}
