// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents a base class for type safe enum-like classes, providing comparison and equality operations
///     as well as methods for retrieving all enumeration instances and looking up instances by id or value.
/// </summary>
[DebuggerDisplay("Id={Id}, Name={Value}")]
public abstract class Enumeration(int id, string value) : Enumeration<int, string>(id, value), IEnumeration
{
    /// <summary>
    ///     Determines whether two Enumeration objects are considered equal.
    /// </summary>
    /// <param name="left">The first Enumeration to compare.</param>
    /// <param name="right">The second Enumeration to compare.</param>
    /// <returns>true if the specified Enumerations are equal; otherwise, false.</returns>
    public static bool operator ==(Enumeration left, Enumeration right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two Enumeration objects for equality.
    /// </summary>
    /// <param name="left">The left Enumeration object to compare.</param>
    /// <param name="right">The right Enumeration object to compare.</param>
    /// <returns>True if the objects are considered equal; otherwise, false.</returns>
    public static bool operator !=(Enumeration left, Enumeration right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Retrieves an enumeration instance of the specified type using the provided identifier.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type to retrieve.</typeparam>
    /// <param name="id">The identifier of the enumeration instance to retrieve.</param>
    /// <returns>The enumeration instance associated with the specified identifier.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the specified identifier does not correspond to any enumeration instance of the specified type.
    /// </exception>
    public static new TEnumeration FromId<TEnumeration>(int id)
        where TEnumeration : IEnumeration
    {
        return Enumeration<int, string>.FromId<TEnumeration>(id);
    }

    /// <summary>
    ///     Retrieves an enumeration instance based on the specified value.
    /// </summary>
    /// <typeparam name="TEnumeration">The enumeration type to retrieve.</typeparam>
    /// <param name="value">The value of the enumeration to retrieve.</param>
    /// <returns>The enumeration instance with the specified value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no enumeration instance with the specified value is found.</exception>
    public static new TEnumeration FromValue<TEnumeration>(string value)
        where TEnumeration : IEnumeration
    {
        return Parse<TEnumeration, string>(value,
            "value",
            e => string.Equals(e.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Retrieves all enumeration instances of a specific type.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of enumeration to retrieve.</typeparam>
    /// <return>An IEnumerable containing all instances of the specified enumeration type.</return>
    public static new IEnumerable<TEnumeration> GetAll<TEnumeration>()
        where TEnumeration : IEnumeration
    {
        return Enumeration<int, string>.GetAll<TEnumeration>();
    }

    /// <summary>
    ///     Checks if the current instance is equal to another <see cref="Enumeration" /> instance.
    /// </summary>
    /// <param name="other">The other <see cref="Enumeration" /> instance to compare with the current instance.</param>
    /// <return>True if the current instance is equal to the specified instance; otherwise, false.</return>
    public new virtual bool Equals(Enumeration other)
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

        return obj is Enumeration other && this.Equals(other);
    }

    /// <summary>
    ///     Returns the hash code for this instance of the Enumeration.
    /// </summary>
    /// <returns>
    ///     A 32-bit signed integer that is the hash code for this instance.
    /// </returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.GetType(), this.Id);
    }

    /// <summary>
    ///     Parses an enumeration of type <typeparamref name="TEnumeration" /> to find an instance matching the provided search
    ///     value.
    /// </summary>
    /// <typeparam name="TEnumeration">The type of the enumeration to parse.</typeparam>
    /// <typeparam name="TSearch">The type of the search value.</typeparam>
    /// <param name="searchValue">The value to search for in the enumeration.</param>
    /// <param name="description">Description of the search value used in the exception message if not found.</param>
    /// <param name="predicate">A predicate function to determine the matching enumeration instance.</param>
    /// <returns>The enumeration instance of type <typeparamref name="TEnumeration" /> that matches the search value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the search value does not match any enumeration instance.</exception>
    private static TEnumeration Parse<TEnumeration, TSearch>(
        TSearch searchValue,
        string description,
        Func<TEnumeration, bool> predicate)
        where TEnumeration : IEnumeration
    {
        return GetAll<TEnumeration>().FirstOrDefault(predicate) ??
            throw new InvalidOperationException(
                $"'{searchValue}' is not a valid {description} for {typeof(TEnumeration)}");
    }
}