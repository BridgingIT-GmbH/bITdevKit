// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Collections.Concurrent;

/// <summary>
/// Represents in memory context.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class InMemoryContext<TEntity>
    where TEntity : class, IEntity
{
    private readonly ConcurrentDictionary<object, TEntity> entities = [];

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryContext</c> class.
    /// </summary>
    public InMemoryContext()
    {
        this.entities = [];
    }

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryContext</c> class.
    /// </summary>
    /// <param name="entities">The entities involved in the operation.</param>
    public InMemoryContext(IEnumerable<TEntity> entities)
    {
        if (entities is null)
        {
            return;
        }

        if (entities.Any(e => e.Id is null))
        {
            throw new ArgumentException("Entity id must cannot be null or empty.", nameof(entities));
        }

        foreach (var entity in entities)
        {
            this.entities.TryAdd(entity.Id, entity);
        }

        // this.entities = [.. entities.ToDictionary(e => e.Id)];
    }

    /// <summary>
    /// Gets the entities.
    /// </summary>
    public ICollection<TEntity> Entities => this.entities.Values;

    /// <summary>
    /// Executes the try add operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryAdd(TEntity entity)
    {
        if (entity is null)
        {
            return false;
        }

        return this.entities.TryAdd(entity.Id, entity);
    }

    /// <summary>
    /// Executes the try get operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(object id, out TEntity entity)
    {
        if (id is null)
        {
            entity = default;

            return false;
        }

        return this.entities.TryGetValue(id, out entity);
    }

    /// <summary>
    /// Executes the try remove operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryRemove(object id, out TEntity entity)
    {
        if (id is null)
        {
            entity = default;

            return false;
        }

        return this.entities.TryRemove(id, out entity);
    }

    /// <summary>
    /// Executes the try update operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryUpdate(TEntity entity)
    {
        if (entity is null || !this.entities.TryGetValue(entity.Id, out entity))
        {
            return false;
        }

        return this.entities.TryUpdate(entity.Id, entity, this.entities[entity.Id]);
    }

    /// <summary>
    /// Executes the clear operation.
    /// </summary>
    public void Clear()
    {
        this.entities.Clear();
    }
}
