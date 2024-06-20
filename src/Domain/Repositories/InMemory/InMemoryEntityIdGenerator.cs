// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class InMemoryEntityIdGenerator<TEntity>(InMemoryContext<TEntity> context) : IEntityIdGenerator<TEntity>
    where TEntity : class, IEntity
{
    private readonly InMemoryContext<TEntity> context = context;

    public bool IsNew(object id)
    {
        if (id is null)
        {
            return true;
        }

        return id switch
        {
            int e => e == 0,
            string e => e.IsNullOrEmpty(),
            Guid e => e == Guid.Empty,
            _ => throw new NotSupportedException($"entity id type {id.GetType().Name} not supported"),
        };
    }

    public void SetNew(TEntity entity)
    {
        EnsureArg.IsNotNull(entity);

        entity.Id = entity switch
        {
            IEntity<int> e => this.context.Entities.Count + 1,
            IEntity<string> e => GuidGenerator.CreateSequential().ToString(),
            IEntity<Guid> e => GuidGenerator.CreateSequential(),
            _ => throw new NotSupportedException($"entity id type {entity.Id.GetType().Name} not supported"),
        };
    }
}