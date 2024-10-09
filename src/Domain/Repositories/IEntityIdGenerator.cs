// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Defines methods for generating and managing entity identifiers.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IEntityIdGenerator<in TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Determines if the given entity identifier is considered new (not yet assigned).
    /// </summary>
    /// <param name="id">The identifier of the entity to check.</param>
    /// <returns>True if the identifier is considered new; otherwise, false.</returns>
    bool IsNew(object id);

    /// <summary>
    ///     Sets the entity id to a new value, depending on the entity's type.
    /// </summary>
    /// <param name="entity">The entity for which a new id needs to be set.</param>
    void SetNew(TEntity entity);
}