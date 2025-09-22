// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

public abstract record EntityRecord<TId> : IEntity<TId>
{
    public TId Id { get; set; }

    object IEntity.Id
    {
        get => this.Id;
        set => this.Id = (TId)value;
    }

    //public static bool operator ==(EntityRecord<TId> left, EntityRecord<TId> right)
    //{
    //    return EqualityComparer<EntityRecord<TId>>.Default.Equals(left, right);
    //}

    //public static bool operator !=(EntityRecord<TId> left, EntityRecord<TId> right)
    //{
    //    return !(left == right);
    //}

    public virtual bool Equals(EntityRecord<TId> other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(other))
        {
            return false;
        }

        if (this.IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(this.Id, other.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetUnproxiedType(this), this.Id);
    }

    // Optional: Override EqualityContract if needed
    protected virtual Type EqualityContract => typeof(EntityRecord<TId>);

    protected static Type GetUnproxiedType(object obj)
    {
        var type = obj.GetType();

        return type.ToString().Contains("Castle.Proxies.") ? type.BaseType : type;
    }

    private bool IsTransient()
    {
        return this.Id is null || this.Id.Equals(default(TId));
    }
}