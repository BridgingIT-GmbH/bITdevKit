// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

using System.Diagnostics;

/// <summary>
///     Represents an abstract base class for entities with a unique identifier.
/// </summary>
/// <typeparam name="TId">The type of the unique identifier for the entity.</typeparam>
[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class Entity<TId> : IEntity<TId>
{
    /// <summary>
    ///     Represents the unique identifier for the entity.
    /// </summary>
    public TId Id { get; set; }

    /// <summary>
    ///     Gets or sets the unique identifier for the object.
    /// </summary>
    /// <value>
    ///     An object representing the unique identifier.
    /// </value>
    object IEntity.Id
    {
        get => this.Id;
        set => this.Id = (TId)value;
    }

    /// <summary>
    ///     Defines the equality operator for comparing two entities.
    /// </summary>
    /// <param name="left">The first entity to compare.</param>
    /// <param name="right">The second entity to compare.</param>
    /// <returns>
    ///     Returns true if the entities are equal; otherwise, false.
    /// </returns>
    public static bool operator ==(Entity<TId> left, Entity<TId> right)
    {
        return Equals(left, null) ? Equals(right, null) : left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two specified instances of <see cref="Entity{TId}" /> are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="Entity{TId}" /> to compare.</param>
    /// <param name="right">The second <see cref="Entity{TId}" /> to compare.</param>
    /// <returns>true if the two <see cref="Entity{TId}" /> instances are not equal; otherwise, false.</returns>
    public static bool operator !=(Entity<TId> left, Entity<TId> right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current entity.
    /// </summary>
    /// <param name="obj">The object to compare with the current entity.</param>
    /// <returns>true if the specified object is equal to the current entity; otherwise, false.</returns>
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

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return (GetUnproxiedType(this).ToString() + this.Id).GetHashCode();
    }

    /// <summary>
    ///     Gets the unproxied type of the given object.
    /// </summary>
    /// <param name="obj">The object from which to obtain the unproxied type.</param>
    /// <returns>The actual type of the object, or its base type if the object is a proxy.</returns>
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
    ///     Determines whether the current entity is transient.
    ///     An entity is considered transient if its identifier has not been set
    ///     or if it equals the default value of its type.
    /// </summary>
    /// <returns>
    ///     True if the entity is transient; otherwise, false.
    /// </returns>
    private bool IsTransient()
    {
        return this.Id is null || this.Id.Equals(default(TId));
    }
}

/// <summary>
///     Defines an abstract base class for entities that have a unique identifier of type TId. Supports equality operations
///     and provides a mechanism to handle proxies.
/// </summary>
/// <typeparam name="TId">The type of the unique identifier for the entity.</typeparam>
[DebuggerDisplay("Type={GetType().Name}, Id={Id}")]
public abstract class Entity<TId, TIdType> : Entity<TId>, IEntity
    where TId : EntityId<TIdType>
{
    /// <summary>
    ///     Gets or sets the unique identifier for an entity.
    /// </summary>
    /// <value>
    ///     A unique object value representing the identifier.
    /// </value>
    public new EntityId<TIdType> Id { get; set; }

    /// <summary>
    ///     Gets or sets the unique identifier for this instance.
    /// </summary>
    /// <value>
    ///     The unique identifier as an object.
    /// </value>
    object IEntity.Id
    {
        get => this.Id;
        set => this.Id = (TId)value;
    }

    //protected AggregateRoot(TId id)
    //{
    //    Id = id;
    //}

    //protected AggregateRoot()
    //{
    //}
}