// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents an abstract base class for creating strongly-typed enumerations.
/// </summary>
/// <typeparam name="TValue">The type of the value represented by the enumeration.</typeparam>
/// <remarks>
///     Initializes a new instance of the <see cref="Enumeration{TValue}" /> class.
///     Defines a set of constants representing the different status codes
///     an operation can result in within the application.
/// </remarks>
public abstract class Enumeration<TValue>(int id, TValue value)
    : Enumeration<int, TValue>(id, value), IEnumeration<TValue>, IEquatable<Enumeration<TValue>>
{
    protected Enumeration() : this(default, default) // for json deserialization
    {
    }

    /// <summary>
    ///     Defines the addition operator for the MyClass type.
    /// </summary>
    /// <param name="left">The first instance of MyClass to add.</param>
    /// <param name="right">The second instance of MyClass to add.</param>
    /// <returns>A new instance of MyClass that is the sum of a and b.</returns>
    public static bool operator ==(Enumeration<TValue> left, Enumeration<TValue> right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    ///     Defines a custom addition operator for the ComplexNumber class.
    /// </summary>
    /// <param name="left">The first complex number.</param>
    /// <param name="right">The second complex number.</param>
    /// <returns>A new ComplexNumber that is the result of adding the real and imaginary parts of both input numbers.</returns>
    public static bool operator !=(Enumeration<TValue> left, Enumeration<TValue> right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Retrieves an enumeration instance of type <typeparamref name="TEnumeration" /> based on the specified identifier.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration.</typeparam>
    /// <param name="id">The identifier of the enumeration.</param>
    /// <returns>An instance of <typeparamref name="TEnumeration" /> that matches the specified identifier.</returns>
    public static new TEnumeration FromId<TEnumeration>(int id)
        where TEnumeration : IEnumeration<TValue>
    {
        return Enumeration<int, TValue>.FromId<TEnumeration>(id);
    }

    /// <summary>
    ///     Parses the given value and returns the corresponding enumeration instance.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type to return.</typeparam>
    /// <param name="value">The value to be parsed into an enumeration instance.</param>
    /// <returns>The enumeration instance corresponding to the provided value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the value does not correspond to any enumeration instance.</exception>
    public static new TEnumeration FromValue<TEnumeration>(TValue value)
        where TEnumeration : IEnumeration<TValue>
    {
        return Parse<TEnumeration, TValue>(value, "value", i => i.Value.Equals(value));
    }

    /// <summary>
    ///     Returns all instances of the specified enumeration type.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration.</typeparam>
    /// <returns>A collection of all instances of the specified enumeration type.</returns>
    public static new IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : IEnumeration<TValue>
    {
        return Enumeration<int, TValue>.GetAll<TEnumeration>();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="other">The Enumeration object to compare with the current instance.</param>
    /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
    public virtual bool Equals(Enumeration<TValue> other)
    {
        if (other is null)
        {
            return false;
        }

        return this.GetType() == other.GetType() && this.Id == other.Id;
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is Enumeration<TValue> other && this.Equals(other);
    }

    /// <summary>
    ///     Returns the hash code for the current object.
    /// </summary>
    /// <returns>
    ///     An integer that represents the hash code for the current object.
    /// </returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.GetType(), this.Id);
    }

    /// <summary>
    ///     Parses the specified search value to find a matching enumeration instance.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration.</typeparam>
    /// <typeparam name="TSearch">The type of the search value.</typeparam>
    /// <param name="searchValue">The value to search for.</param>
    /// <param name="description">The description of the search value.</param>
    /// <param name="predicate">The predicate function to determine a match.</param>
    /// <returns>A matching enumeration instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no matching enumeration is found.</exception>
    private static TEnumeration Parse<TEnumeration, TSearch>(
        TSearch searchValue,
        string description,
        Func<TEnumeration, bool> predicate)
        where TEnumeration : IEnumeration<TValue>
    {
        return GetAll<TEnumeration>().FirstOrDefault(predicate) ??
            throw new InvalidOperationException(
                $"'{searchValue}' is not a valid {description} for {typeof(TEnumeration)}");
    }
}