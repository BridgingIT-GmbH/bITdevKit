// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents a value object that can be compared to other value objects for ordering purposes.
/// </summary>
/// <remarks>
///     A value object is immutable and distinguishable only by the state of its properties.
///     ComparableValueObject extends this concept by incorporating comparison capabilities.
/// </remarks>
public abstract class ComparableValueObject : ValueObject, IComparable
{
    /// <summary>
    ///     Defines a type conversion operator from a specified source type to the target type.
    ///     This operator provides a way to convert instances of the source type to the target type.
    /// </summary>
    public static bool operator <(ComparableValueObject left, ComparableValueObject right)
    {
        return left is null ? right != null : left.CompareTo(right) < 0;
    }

    /// <summary>
    ///     Determines if the left <see cref="ComparableValueObject" /> is less than or equal to the right
    ///     <see cref="ComparableValueObject" />.
    /// </summary>
    /// <param name="left">The left <see cref="ComparableValueObject" /> to compare.</param>
    /// <param name="right">The right <see cref="ComparableValueObject" /> to compare.</param>
    /// <returns>
    ///     true if the left <see cref="ComparableValueObject" /> is less than or equal to the right
    ///     <see cref="ComparableValueObject" />; otherwise, false.
    /// </returns>
    public static bool operator <=(ComparableValueObject left, ComparableValueObject right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    /// <summary>
    ///     Determines whether one <see cref="ComparableValueObject" /> is greater than another.
    /// </summary>
    /// <param name="left">The first object to compare.</param>
    /// <param name="right">The second object to compare.</param>
    /// <returns>true if the first object is greater than the second object; otherwise, false.</returns>
    public static bool operator >(ComparableValueObject left, ComparableValueObject right)
    {
        return left != null && left.CompareTo(right) > 0;
    }

    /// <summary>
    ///     Determines whether the left operand is less than the right operand.
    /// </summary>
    public static bool operator >=(ComparableValueObject left, ComparableValueObject right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

    /// <summary>
    ///     Compares the current object with another object of the same type.
    /// </summary>
    /// <param name="other">The object to compare with the current object.</param>
    /// <returns>An integer that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(object other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(other))
        {
            throw new InvalidOperationException();
        }

        return this.CompareTo(other as ComparableValueObject);
    }

    /// <summary>
    ///     Gets the atomic values of the properties important for the equality and comparison.
    /// </summary>
    /// <returns>
    ///     An enumerable of the comparable atomic values.
    /// </returns>
    protected abstract IEnumerable<IComparable> GetComparableAtomicValues();

    /// <summary>
    ///     Compares the current object with another ComparableValueObject and returns an integer that indicates
    ///     whether the current object precedes, follows, or occurs in the same position in the sort order as
    ///     the other ComparableValueObject.
    /// </summary>
    /// <param name="other">The ComparableValueObject to compare with the current object.</param>
    /// <return>
    ///     An integer that indicates the relative order of the objects being compared. The return value
    ///     has these meanings:
    ///     Less than zero: This object is less than the other.
    ///     Zero: This object is equal to the other.
    ///     Greater than zero: This object is greater than the other.
    /// </return>
    private int CompareTo(ComparableValueObject other)
    {
        using var values = this.GetComparableAtomicValues().GetEnumerator();
        using var otherValues = other.GetComparableAtomicValues().GetEnumerator();
        while (true)
        {
            var x = values.MoveNext();
            var y = otherValues.MoveNext();
            if (x != y)
            {
                throw new InvalidOperationException();
            }

            if (x)
            {
                var c = values.Current?.CompareTo(otherValues.Current) ?? 0;
                if (c != 0)
                {
                    return c;
                }
            }
            else
            {
                break;
            }
        }

        return 0;
    }
}