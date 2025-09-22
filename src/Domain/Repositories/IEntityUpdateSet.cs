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
    /// Sets a property to a given value for all matching entities.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="property">The property selector.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>The update set for chaining.</returns>
    /// <example>
    /// <code>
    /// set => set
    ///     .Set(u => u.IsActive, false)
    ///     .Set(u => u.Status, "Inactive")
    /// </code>
    /// </example>
    IEntityUpdateSet<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, TProperty value);

    IEntityUpdateSet<TEntity> Set<TProperty>(Expression<Func<TEntity, TProperty>> property, Expression<Func<TEntity, TProperty>> valueFactory);
}
