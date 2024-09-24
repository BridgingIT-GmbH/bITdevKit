// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using Domain.Model;
using Domain.Repositories;

public class CosmosSqlGenericRepositoryOptionsBuilder<TEntity>
    : OptionsBuilderBase<CosmosSqlGenericRepositoryOptions<TEntity>, CosmosSqlGenericRepositoryOptionsBuilder<TEntity>>
    where TEntity : class, IEntity
{
    public CosmosSqlGenericRepositoryOptionsBuilder<TEntity> Provider(ICosmosSqlProvider<TEntity> provider)
    {
        this.Target.Provider = provider;
        return this;
    }

    public CosmosSqlGenericRepositoryOptionsBuilder<TEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;
        return this;
    }

    public CosmosSqlGenericRepositoryOptionsBuilder<TEntity> IdGenerator(IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;
        return this;
    }
}

public class CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity>
    : OptionsBuilderBase<CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity>,
        CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity>>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> Provider(
        ICosmosSqlProvider<TDatabaseEntity> provider)
    {
        this.Target.Provider = provider;
        return this;
    }

    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;
        return this;
    }

    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> IdGenerator(
        IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;
        return this;
    }

    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;
        return this;
    }
}