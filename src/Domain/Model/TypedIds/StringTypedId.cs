// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;

public abstract class StringTypedId(string value) : IEquatable<StringTypedId>, IComparable<StringTypedId>
{
    public string Value { get; } = value;

    public static implicit operator string(StringTypedId typedId) => typedId.Value;

    public static bool operator ==(StringTypedId left, StringTypedId right)
    {
        if (Equals(left, null))
        {
            return Equals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator !=(StringTypedId left, StringTypedId right)
    {
        return !(left == right);
    }

    //public static TypedId For(string value) => new TypedId(value);

    public override bool Equals(object other)
    {
        if (other is null)
        {
            return false;
        }

        return other is StringTypedId obj && this.Equals(obj);
    }

    public override int GetHashCode()
    {
        return this.Value.GetHashCode();
    }

    public bool Equals(StringTypedId other)
    {
        return this.Value == other?.Value;
    }

    public int CompareTo(StringTypedId other)
    {
        return this.Value.CompareTo(other.Value);
    }

    public bool IsEmpty()
    {
        return this.Value?.Length == 0;
    }

    public override string ToString()
    {
        return this.Value;
    }
}