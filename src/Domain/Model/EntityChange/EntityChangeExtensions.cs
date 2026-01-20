// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;
/// <summary>
/// Provides extension methods for entities to enable fluent,
/// transactional-style state changes with automatic change tracking and event registration.
/// </summary>
public static class EntityChangeExtensions
{
    /// <summary>
    /// Initiates a fluent change transaction on an entity.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A <see cref="EntityChangeBuilder{TEntity}"/> for fluent configuration.</returns>
    /// <example>
    /// <code>
    /// return this.Change()
    ///     .Set(c => c.Name, "New Name")
    ///     .Apply();
    /// </code>
    /// </example>
    public static EntityChangeBuilder<TEntity> Change<TEntity>(this TEntity entity)
        where TEntity : IEntity
    {
        return new EntityChangeBuilder<TEntity>(entity);
    }
}