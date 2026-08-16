// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Builds active entity in memory provider options configuration.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class ActiveEntityInMemoryProviderOptionsBuilder<TEntity>
    : OptionsBuilderBase<ActiveEntityInMemoryProviderOptions<TEntity>, ActiveEntityInMemoryProviderOptionsBuilder<TEntity>>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes the context operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="context">The context used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> Context(InMemoryContext<TEntity> context)
    {
        this.Target.Context = context;

        return this;
    }

    /// <summary>
    /// Executes the publish events operation.
    /// </summary>
    /// <param name="publishEvents">The publish events used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    /// <summary>
    /// Executes the id generator operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="idGenerator">The id generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> IdGenerator(IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;

        return this;
    }

    /// <summary>
    /// Executes the enable optimistic concurrency operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }

    /// <summary>
    /// Executes the version generator operation.
    /// </summary>
    /// <typeparam name="Guid">The guid type.</typeparam>
    /// <param name="generator">The generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ActiveEntityInMemoryProviderOptionsBuilder<TEntity> VersionGenerator(Func<Guid> generator)
    {
        this.Target.VersionGenerator = generator;

        return this;
    }
}
