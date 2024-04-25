// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;

/// <summary>
/// Base class for Entity.
/// </summary>
/// <remarks>Template: https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces.</remarks>
[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class Entity<TId> : IEntity<TId>
{
    public TId Id { get; set; }

    object IEntity.Id
    {
        get { return this.Id; }
        set { this.Id = (TId)value; }
    }

    public static bool operator ==(Entity<TId> left, Entity<TId> right)
    {
        return Equals(left, null) ? Equals(right, null) : left.Equals(right);
    }

    public static bool operator !=(Entity<TId> left, Entity<TId> right)
    {
        return !(left == right);
    }

    public override bool Equals(object obj)
    {
        if (obj is not Entity<TId> other)
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

        return this.Id.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return (GetUnproxiedType(this).ToString() + this.Id).GetHashCode();
    }

    protected static Type GetUnproxiedType(object obj)
    {
        var type = obj.GetType();

        if (type.ToString().Contains("Castle.Proxies."))
        {
            return type.BaseType;
        }

        return type;
    }

    private bool IsTransient()
    {
        return this.Id is null || this.Id.Equals(default(TId));
    }
}

[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class Entity<TId, TIdType> : Entity<TId>, IEntity
    where TId : EntityId<TIdType>
{
    public new EntityId<TIdType> Id { get; set; }

    object IEntity.Id
    {
        get { return this.Id; }
        set { this.Id = (TId)value; }
    }

    //protected AggregateRoot(TId id)
    //{
    //    Id = id;
    //}

    //protected AggregateRoot()
    //{
    //}
}