// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;
using System.Reflection;

/// <summary>
///     Represents an abstraction for a generic enumeration with an identifier and a value.
/// </summary>
/// <remarks>
///     This class can be used to create enumerations where each item has a unique identifier and a value.
///     Instances of this class are considered equal if they are of the same type and have the same identifier.
/// </remarks>
/// <typeparam name="TId">Type of the identifier, which must implement IComparable.</typeparam>
/// <typeparam name="TValue">Type of the value, which must implement IComparable.</typeparam>
[DebuggerDisplay("Id={Id}")]
public abstract class Enumeration<TId, TValue>(TId id, TValue value) : IEnumeration<TId, TValue>
    where TId : IComparable
{
    /// <summary>
    ///     Gets the unique identifier of the enumeration item.
    /// </summary>
    public TId Id { get; } = id;

    /// <summary>
    ///     Gets the value associated with the enumeration.
    ///     This property holds the value part of the enumeration, which can be any type that implements IComparable.
    ///     The value is set at the time of enumeration creation and cannot be changed.
    /// </summary>
    public TValue Value { get; } = value;

    /// <summary>
    ///     Retrieves all instances of the enumeration type.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration to retrieve instances for.</typeparam>
    /// <returns>An enumerable collection of all enumeration instances of the specified type.</returns>
    public static IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return typeof(TEnumeration).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<TEnumeration>();
    }

    /// <summary>
    ///     Retrieves an enumeration type instance based on the provided identifier.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration.</typeparam>
    /// <param name="id">The identifier of the enumeration type instance to retrieve.</param>
    /// <returns>The enumeration type instance associated with the specified identifier.</returns>
    public static TEnumeration FromId<TEnumeration>(TId id)
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return Parse<TEnumeration, TId>(id, "id", i => i.Id.Equals(id));
    }

    /// <summary>
    ///     Retrieves an enumeration item by its associated value.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration class.</typeparam>
    /// <param name="value">The value to match with the enumeration's value.</param>
    /// <returns>The enumeration item that matches the provided value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value does not match any enumeration item.</exception>
    public static TEnumeration FromValue<TEnumeration>(TValue value)
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return Parse<TEnumeration, TValue>(value, "value", i => i.Value.Equals(value));
    }

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <return>
    ///     A string that represents the current object.
    /// </return>
    public override string ToString()
    {
        return this.Value.ToString();
    }

    /// <summary>
    ///     Determines whether the specified <see cref="Enumeration{TId, TValue}" /> is equal to the current
    ///     <see cref="Enumeration{TId, TValue}" />.
    /// </summary>
    /// <param name="other">
    ///     The <see cref="Enumeration{TId, TValue}" /> to compare with the current
    ///     <see cref="Enumeration{TId, TValue}" />.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the specified <see cref="Enumeration{TId, TValue}" /> is equal to the current
    ///     <see cref="Enumeration{TId, TValue}" />; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool Equals(Enumeration<TId, TValue> other)
    {
        if (other is null)
        {
            return false;
        }

        return this.GetType() == other.GetType() && this.Id.Equals(other.Id);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="other">The enumeration to compare with the current enumeration.</param>
    /// <returns>true if the specified enumeration is equal to the current enumeration; otherwise, false.</returns>
    public bool Equals(Enumeration other)
    {
        return other != null && this.GetType() == other.GetType() && this.Id.Equals(other.Id);
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

        return obj is Enumeration<TId, TValue> other && this.Equals(other);
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
    ///     Compares the current instance with another object of the same type and returns an
    ///     integer that indicates whether the current instance precedes, follows, or occurs
    ///     in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="other">The object to compare with the current instance.</param>
    /// <returns>
    ///     A value that indicates the relative order of the objects being compared.
    ///     The return value has these meanings:
    ///     Less than zero: This instance precedes <paramref name="other" /> in the sort order.
    ///     Zero: This instance occurs in the same position in the sort order as <paramref name="other" />.
    ///     Greater than zero: This instance follows <paramref name="other" /> in the sort order.
    ///     If <paramref name="other" /> is null, the return value is 1.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="other" /> is not of type
    ///     <see cref="Enumeration{TId, TValue}" />.
    /// </exception>
    public int CompareTo(object other)
    {
        if (other is null)
        {
            return 1;
        }

        return other is Enumeration<TId, TValue> otherEnumeration
            ? this.Id.CompareTo(otherEnumeration.Id)
            : throw new ArgumentException($"Object must be of type {nameof(Enumeration<TId, TValue>)}");
    }

    /// <summary>
    ///     Parses the provided search value to find an enumeration entry that matches the given predicate.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration.</typeparam>
    /// <typeparam name="TSearch">The type of the search value.</typeparam>
    /// <param name="searchValue">The value to search for in the enumeration.</param>
    /// <param name="description">A description of the search value (e.g., "id" or "value").</param>
    /// <param name="predicate">A function that defines the conditions to match an enumeration entry.</param>
    /// <returns>The matched enumeration entry if found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no matching enumeration entry is found.</exception>
    private static TEnumeration Parse<TEnumeration, TSearch>(
        TSearch searchValue,
        string description,
        Func<TEnumeration, bool> predicate)
        where TEnumeration : IEnumeration<TId, TValue>
    {
        return GetAll<TEnumeration>().FirstOrDefault(predicate) ??
            throw new InvalidOperationException(
                $"'{searchValue}' is not a valid {description} for {typeof(TEnumeration)}");
    }
}