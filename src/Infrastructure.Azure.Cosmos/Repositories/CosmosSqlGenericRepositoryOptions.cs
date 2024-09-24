// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using Domain.Model;
using Domain.Repositories;

public class CosmosSqlGenericRepositoryOptions<TEntity> : OptionsBase
    where TEntity : class, IEntity
{
    public ICosmosSqlProvider<TEntity> Provider { get; set; }

    public bool PublishEvents { get; set; } = true;

    public IEntityIdGenerator<TEntity> IdGenerator { get; set; } = new EntityGuidIdGenerator<TEntity>();
}

public class CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity> : OptionsBase
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    public ICosmosSqlProvider<TDatabaseEntity> Provider { get; set; }

    public bool PublishEvents { get; set; } = true;

    public IEntityIdGenerator<TEntity> IdGenerator { get; set; } = new EntityGuidIdGenerator<TEntity>();

    public IEntityMapper Mapper { get; set; }
}