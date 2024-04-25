// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using EnsureThat;

public class EntityGuidIdGenerator<TEntity> : IEntityIdGenerator<TEntity>
    where TEntity : class, IEntity
{
    public bool IsNew(object id)
    {
        if (id == null)
        {
            return true;
        }

        return id.To<Guid>() == Guid.Empty;
    }

    public void SetNew(TEntity entity)
    {
        EnsureArg.IsNotNull(entity);

        entity.Id = entity switch
        {
            IEntity<string> e => GuidGenerator.CreateSequential().ToString(),
            IEntity<Guid> e => GuidGenerator.CreateSequential(),
            _ => throw new NotSupportedException($"entity id type {entity.Id.GetType().Name} not supported"),
        };
    }
}