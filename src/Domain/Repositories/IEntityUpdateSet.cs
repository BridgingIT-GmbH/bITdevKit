// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;

/// <summary>
/// Defines a builder for specifying property updates in a type-safe way.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityUpdateSet<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Adds a property assignment with a constant value.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector expression.</param>
    /// <param name="value">The constant value to assign to the property.</param>
    /// <returns>The current <see cref="IEntityUpdateSet{TEntity}"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set.Set(u => u.IsActive, false);
    /// </code>
    /// </example>
    IEntityUpdateSet<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, TProperty value);

    /// <summary>
    /// Adds a property assignment with a computed value based on the entity itself.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being updated.</typeparam>
    /// <param name="property">The property selector expression.</param>
    /// <param name="valueFactory">An expression that computes the new value from the entity.</param>
    /// <returns>The current <see cref="IEntityUpdateSet{TEntity}"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set.Set(u => u.LoginCount, u => u.LoginCount + 1);
    /// </code>
    /// </example>
    IEntityUpdateSet<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, Expression<Func<TEntity, TProperty>> valueFactory);
}
