// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System;

[Obsolete("To be removed. Please use TypedId")]
public abstract class TypedIdValueBase : GuidTypedId
{
    protected TypedIdValueBase(Guid value)
        : base(value)
    {
    }
}

public abstract class GuidTypedId : IEquatable<GuidTypedId>, IComparable<GuidTypedId>
{
    protected GuidTypedId(Guid value)
    {
        this.Value = value;
    }

    public Guid Value { get; }

    public static implicit operator Guid(GuidTypedId typedId) => typedId.Value;

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