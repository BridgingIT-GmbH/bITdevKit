// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Common;
using Model;

public class InMemoryRepositoryOptionsBuilder<TEntity>
    : OptionsBuilderBase<InMemoryRepositoryOptions<TEntity>, InMemoryRepositoryOptionsBuilder<TEntity>>
    where TEntity : class, IEntity
{
    public InMemoryRepositoryOptionsBuilder<TEntity> Context(InMemoryContext<TEntity> context)
    {
        this.Target.Context = context;

        return this;
    }

    public InMemoryRepositoryOptionsBuilder<TEntity> Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;

        return this;
    }

    public InMemoryRepositoryOptionsBuilder<TEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    public InMemoryRepositoryOptionsBuilder<TEntity> IdGenerator(IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;

        return this;
    }
}