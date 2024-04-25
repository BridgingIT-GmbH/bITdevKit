// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;

public abstract class IntTypedId : IEquatable<IntTypedId>, IComparable<IntTypedId>
{
    protected IntTypedId(int value)
    {
        this.Value = value;
    }

    public int Value { get; }

    public static implicit operator int(IntTypedId typedId) => typedId.Value;

    public static bool operator ==(IntTypedId left, IntTypedId right)
    {
        if (Equals(left, null))
        {
            return Equals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator !=(IntTypedId left, IntTypedId right)
    {
        return !(left == right);
    }

    //public static TypedId For(int value) => new TypedId(value);

    public override bool Equals(object other)
    {
        if (other is null)
        {
            return false;
        }

        return other is IntTypedId obj && this.Equals(obj);
    }

    public override int GetHashCode()
    {
        return this.Value.GetHashCode();
    }

    public bool Equals(IntTypedId other)
    {
        return this.Value == other?.Value;
    }

    public int CompareTo(IntTypedId other)
    {
        return this.Value.CompareTo(other.Value);
    }

    public bool IsEmpty()
    {
        return this.Value == 0;
    }

    public override string ToString()
    {
        return this.Value.ToString();
    }
}