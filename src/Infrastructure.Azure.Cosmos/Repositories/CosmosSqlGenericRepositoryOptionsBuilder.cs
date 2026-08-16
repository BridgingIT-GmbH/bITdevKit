// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Common;
using Domain.Model;
using Domain.Repositories;

/// <summary>
/// Builds cosmos sql generic repository options configuration.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class CosmosSqlGenericRepositoryOptionsBuilder<TEntity>
    : OptionsBuilderBase<CosmosSqlGenericRepositoryOptions<TEntity>, CosmosSqlGenericRepositoryOptionsBuilder<TEntity>>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes the provider operation.
    /// </summary>
    /// <param name="provider">The provider used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlGenericRepositoryOptionsBuilder<TEntity> Provider(ICosmosSqlProvider<TEntity> provider)
    {
        this.Target.Provider = provider;

        return this;
    }

    /// <summary>
    /// Publishes events.
    /// </summary>
    /// <param name="publishEvents">The publish events used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlGenericRepositoryOptionsBuilder<TEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    /// <summary>
    /// Executes the id generator operation.
    /// </summary>
    /// <param name="idGenerator">The id generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlGenericRepositoryOptionsBuilder<TEntity> IdGenerator(IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;

        return this;
    }
}

/// <summary>
/// Builds cosmos sql repository options configuration.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
public class CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity>
    : OptionsBuilderBase<CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity>,
        CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity>>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    /// <summary>
    /// Executes the provider operation.
    /// </summary>
    /// <param name="provider">The provider used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> Provider(
        ICosmosSqlProvider<TDatabaseEntity> provider)
    {
        this.Target.Provider = provider;

        return this;
    }

    /// <summary>
    /// Publishes events.
    /// </summary>
    /// <param name="publishEvents">The publish events used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    /// <summary>
    /// Executes the id generator operation.
    /// </summary>
    /// <param name="idGenerator">The id generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> IdGenerator(
        IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;

        return this;
    }

    /// <summary>
    /// Executes the mapper operation.
    /// </summary>
    /// <param name="mapper">The mapper used to transform values.</param>
    /// <returns>The result of the operation.</returns>
    public CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity> Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;

        return this;
    }
}
