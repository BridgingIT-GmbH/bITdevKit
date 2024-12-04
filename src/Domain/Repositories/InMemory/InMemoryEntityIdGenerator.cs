// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

public class InMemoryEntityIdGenerator<TEntity>(InMemoryContext<TEntity> context) : IEntityIdGenerator<TEntity>
    where TEntity : class, IEntity
{
    private readonly InMemoryContext<TEntity> context = context;

    public bool IsNew(object id)
    {
        return id switch
        {
            null => true,
            int e => e == 0,
            long e => e == 0,
            string e => e.IsNullOrEmpty(),
            Guid e => e == Guid.Empty,
            _ => throw new NotSupportedException($"entity id type {id.GetType().Name} not supported")
        };
    }

    public void SetNew(TEntity entity)
    {
        EnsureArg.IsNotNull(entity);

        entity.Id = entity switch
        {
            IEntity<int> => this.context.Entities.Count + 1,
            IEntity<long> => this.context.Entities.Count + 1,
            IEntity<string> => GuidGenerator.CreateSequential().ToString(),
            IEntity<Guid> => GuidGenerator.CreateSequential(),
            _ => throw new NotSupportedException($"entity id type {entity.Id.GetType().Name} not supported")
        };
    }
}