// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

public abstract class LongTypedId(long value) : IEquatable<LongTypedId>, IComparable<LongTypedId>
{
    public long Value { get; } = value;

    public static implicit operator long(LongTypedId typedId)
    {
        return typedId.Value;
    }

    public static bool operator ==(LongTypedId left, LongTypedId right)
    {
        if (Equals(left, null))
        {
            return Equals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator !=(LongTypedId left, LongTypedId right)
    {
        return !(left == right);
    }

    //public static TypedId For(long value) => new TypedId(value);

    public override bool Equals(object other)
    {
        if (other is null)
        {
            return false;
        }

        return other is LongTypedId obj && this.Equals(obj);
    }

    public override int GetHashCode()
    {
        return this.Value.GetHashCode();
    }

    public bool Equals(LongTypedId other)
    {
        return this.Value == other?.Value;
    }

    public int CompareTo(LongTypedId other)
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