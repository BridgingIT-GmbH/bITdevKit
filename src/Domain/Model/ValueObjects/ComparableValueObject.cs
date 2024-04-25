// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;
using System.Collections.Generic;

[Obsolete("To be removed. Please use ComparableValueObject")]
public abstract class ValueObjectComparable : ComparableValueObject
{
}

public abstract class ComparableValueObject : ValueObject, IComparable
{
    public static bool operator <(ComparableValueObject left, ComparableValueObject right)
    {
        return left is null ? right is object : left.CompareTo(right) < 0;
    }

    public static bool operator <=(ComparableValueObject left, ComparableValueObject right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >(ComparableValueObject left, ComparableValueObject right)
    {
        return left is object && left.CompareTo(right) > 0;
    }

    public static bool operator >=(ComparableValueObject left, ComparableValueObject right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

    public int CompareTo(ComparableValueObject other)
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
                var c = values.Current.CompareTo(otherValues.Current);
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

    public int CompareTo(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return 0;
        }

        if (obj is null)
        {
            return 1;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(obj))
        {
            throw new InvalidOperationException();
        }

        return this.CompareTo(obj as ComparableValueObject);
    }

    /// <summary>
    /// Gets the atomic values of the properties important for the equality.
    /// </summary>
    protected abstract IEnumerable<IComparable> GetComparableAtomicValues();
}