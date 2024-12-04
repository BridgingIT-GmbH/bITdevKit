// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Collections.Concurrent;

public class InMemoryContext<TEntity>
    where TEntity : class, IEntity
{
    private readonly ConcurrentDictionary<object, TEntity> entities;

    public InMemoryContext()
    {
        this.entities = new ConcurrentDictionary<object, TEntity>();
    }

    public InMemoryContext(IEnumerable<TEntity> entities)
    {
        if (entities is null)
        {
            this.entities = new ConcurrentDictionary<object, TEntity>();

            return;
        }

        if (entities.Any(e => e.Id is null))
        {
            throw new ArgumentException("Entity id must cannot be null or empty.", nameof(entities));
        }

        this.entities = new ConcurrentDictionary<object, TEntity>(
            entities.ToDictionary(e => e.Id));
    }

    public ICollection<TEntity> Entities => this.entities.Values;

    public bool TryAdd(TEntity entity)
    {
        if (entity is null)
        {
            return false;
        }

        return this.entities.TryAdd(entity.Id, entity);
    }

    public bool TryGet(object id, out TEntity entity)
    {
        if (id is null)
        {
            entity = default;

            return false;
        }

        return this.entities.TryGetValue(id, out entity);
    }

    public bool TryRemove(object id, out TEntity entity)
    {
        if (id is null)
        {
            entity = default;

            return false;
        }

        return this.entities.TryRemove(id, out entity);
    }

    public bool TryUpdate(TEntity entity)
    {
        if (entity is null || !this.entities.TryGetValue(entity.Id, out entity))
        {
            return false;
        }

        return this.entities.TryUpdate(entity.Id, entity, this.entities[entity.Id]);
    }

    public void Clear()
    {
        this.entities.Clear();
    }
}