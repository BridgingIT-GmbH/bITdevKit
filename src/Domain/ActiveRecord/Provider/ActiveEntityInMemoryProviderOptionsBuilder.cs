// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Repositories;

public class ActiveEntityInMemoryProviderOptionsBuilder<TEntity>
    : OptionsBuilderBase<ActiveEntityInMemoryProviderOptions<TEntity>, ActiveEntityInMemoryProviderOptionsBuilder<TEntity>>
    where TEntity : class, IEntity
{
    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> Context(InMemoryContext<TEntity> context)
    {
        this.Target.Context = context;

        return this;
    }

    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> IdGenerator(IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;

        return this;
    }

    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }

    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> VersionGenerator(Func<Guid> generator)
    {
        this.Target.VersionGenerator = generator;

        return this;
    }
}