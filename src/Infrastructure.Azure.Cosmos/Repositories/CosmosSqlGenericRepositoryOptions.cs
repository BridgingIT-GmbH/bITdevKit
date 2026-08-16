// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using Domain.Model;
using Domain.Repositories;

/// <summary>
/// Configures cosmos sql generic repository.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class CosmosSqlGenericRepositoryOptions<TEntity> : OptionsBase
    where TEntity : class, IEntity
{
    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public ICosmosSqlProvider<TEntity> Provider { get; set; }

    /// <summary>
    /// Gets or sets the publish events.
    /// </summary>
    public bool PublishEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets the id generator.
    /// </summary>
    public IEntityIdGenerator<TEntity> IdGenerator { get; set; } = new EntityGuidIdGenerator<TEntity>();
}

/// <summary>
/// Configures cosmos sql repository.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
public class CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity> : OptionsBase
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public ICosmosSqlProvider<TDatabaseEntity> Provider { get; set; }

    /// <summary>
    /// Gets or sets the publish events.
    /// </summary>
    public bool PublishEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets the id generator.
    /// </summary>
    public IEntityIdGenerator<TEntity> IdGenerator { get; set; } = new EntityGuidIdGenerator<TEntity>();

    /// <summary>
    /// Gets or sets the mapper.
    /// </summary>
    public IEntityMapper Mapper { get; set; }
}
