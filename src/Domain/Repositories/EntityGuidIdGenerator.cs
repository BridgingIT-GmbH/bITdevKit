// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Common;
using Model;

/// <summary>
///     Provides functionality to generate and validate GUID-based IDs for entities.
/// </summary>
/// <typeparam name="TEntity">The type of the entity for which IDs are generated.</typeparam>
public class EntityGuidIdGenerator<TEntity> : IEntityIdGenerator<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Determines whether the specified identifier represents a new entity.
    /// </summary>
    /// <param name="id">The identifier to evaluate.</param>
    /// <returns>True if the identifier is either null or an empty Guid, otherwise false.</returns>
    public bool IsNew(object id)
    {
        if (id is null)
        {
            return true;
        }

        return id.To<Guid>() == Guid.Empty;
    }

    /// <summary>
    ///     Sets a new identifier for the specified entity.
    /// </summary>
    /// <param name="entity">The entity for which to set a new identifier.</param>
    public void SetNew(TEntity entity)
    {
        EnsureArg.IsNotNull(entity);

        entity.Id = entity switch
        {
            IEntity<string> => GuidGenerator.CreateSequential().ToString(),
            IEntity<Guid> => GuidGenerator.CreateSequential(),
            _ => throw new NotSupportedException($"Entity id of type {entity.Id.GetType().Name} not supported")
        };
    }
}