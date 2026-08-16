// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Represents entity record.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public abstract record EntityRecord<TId> : IEntity<TId>
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
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

    /// <summary>
    /// Executes the equals operation.
    /// </summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <param name="other">The other used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(GetUnproxiedType(this), this.Id);
    }

    // Optional: Override EqualityContract if needed
    /// <summary>
    /// Gets the equality contract.
    /// </summary>
    protected virtual Type EqualityContract => typeof(EntityRecord<TId>);

    /// <summary>
    /// Gets unproxied type.
    /// </summary>
    /// <param name="obj">The obj used by the operation.</param>
    /// <returns>The result of the operation.</returns>
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
