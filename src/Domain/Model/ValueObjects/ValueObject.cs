// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     A Value Object is an immutable type that is distinguishable only by the state of
///     its properties. That is, unlike an Entity, which has a unique identifier and remains
///     distinct even if its properties are otherwise identical, two Value Objects with the
///     exact same properties can be considered equal.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    private int? cachedHashCode;

    /// <summary>
    ///     Determines whether two specified ValueObject instances are equal.
    /// </summary>
    /// <param name="left">The first ValueObject to compare.</param>
    /// <param name="right">The second ValueObject to compare.</param>
    /// <returns>true if the two ValueObject instances are considered equal; otherwise, false.</returns>
    public static bool operator ==(ValueObject left, ValueObject right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two <see cref="ValueObject" /> instances are equal.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns><c>true</c> if the objects are considered equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(ValueObject left, ValueObject right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    ///     <c>true</c> if the current object is equal to the other parameter; otherwise, false.
    /// </returns>
    public bool Equals(ValueObject other)
    {
        if (other is null)
        {
            return false;
        }

        using var thisValues = this.GetAtomicValues().GetEnumerator();
        using var otherValues = other.GetAtomicValues().GetEnumerator();

        while (thisValues.MoveNext() && otherValues.MoveNext())
        {
            if (thisValues.Current is null ^ otherValues.Current is null)
            {
                return false;
            }

            if (thisValues.Current?.Equals(otherValues.Current) == false)
            {
                return false;
            }
        }

        return !thisValues.MoveNext() && !otherValues.MoveNext();
    }

    /// <summary>
    ///     Indicates whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    ///     true if the specified object is equal to this instance; otherwise, false.
    /// </returns>
    public override bool Equals(object obj)
    {
        // Source: https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/implement-value-objects
        if (obj is null)
        {
            return false;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(obj))
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

    /// <summary>
    ///     Determines whether two <see cref="ValueObject" /> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="ValueObject" /> to compare.</param>
    /// <param name="right">The second <see cref="ValueObject" /> to compare.</param>
    /// <returns>
    ///     <c>true</c> if the specified <see cref="ValueObject" /> instances are equal; otherwise, <c>false</c>.
    /// </returns>
    protected static bool EqualOperator(ValueObject left, ValueObject right)
    {
        if (left is null ^ right is null)
        {
            return false;
        }

        return left?.Equals(right) != false;
    }

    /// <summary>
    ///     Determines whether two ValueObject instances are not equal.
    /// </summary>
    /// <param name="left">The left ValueObject to compare.</param>
    /// <param name="right">The right ValueObject to compare.</param>
    /// <returns><c>true</c> if the left instance is not equal to the right instance; otherwise, <c>false</c>.</returns>
    protected static bool NotEqualOperator(ValueObject left, ValueObject right)
    {
        return !EqualOperator(left, right);
    }

    /// <summary>
    ///     Gets the unproxied type of the specified object.
    /// </summary>
    /// <param name="obj">The object to get the unproxied type of.</param>
    /// <returns>
    ///     The original type of the object if it is a proxy, otherwise the actual type.
    /// </returns>
    protected static Type GetUnproxiedType(object obj)
    {
        var type = obj.GetType();

        if (type.ToString().Contains("Castle.Proxies."))
        {
            return type.BaseType;
        }

        return type;
    }

    /// <summary>
    ///     Gets the atomic values of the properties important for the equality.
    /// </summary>
    /// <returns>
    ///     A collection of objects representing the atomic values of the properties.
    /// </returns>
    protected abstract IEnumerable<object> GetAtomicValues();
}