// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

public abstract class GuidTypedId(Guid value) : IEquatable<GuidTypedId>, IComparable<GuidTypedId>
{
    public Guid Value { get; } = value;

    public static implicit operator Guid(GuidTypedId typedId)
    {
        return typedId.Value;
    }

    public static bool operator ==(GuidTypedId left, GuidTypedId right)
    {
        if (Equals(left, null))
        {
            return Equals(right, null);
        }

        return left.Equals(right);
    }

    public static bool operator !=(GuidTypedId left, GuidTypedId right)
    {
        return !(left == right);
    }

    //public static TypedId For(Guid value) => new TypedId(value);

    public override bool Equals(object other)
    {
        if (other is null)
        {
            return false;
        }

        return other is GuidTypedId obj && this.Equals(obj);
    }

    public override int GetHashCode()
    {
        return this.Value.GetHashCode();
    }

    public bool Equals(GuidTypedId other)
    {
        return this.Value == other?.Value;
    }

    public int CompareTo(GuidTypedId other)
    {
        return this.Value.CompareTo(other.Value);
    }

    public bool IsEmpty()
    {
        return this.Value == Guid.Empty;
    }

    public override string ToString()
    {
        return this.Value.ToString();
    }
}