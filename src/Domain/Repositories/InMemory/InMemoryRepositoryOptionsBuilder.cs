// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Builds in memory repository options configuration.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class InMemoryRepositoryOptionsBuilder<TEntity>
    : OptionsBuilderBase<InMemoryRepositoryOptions<TEntity>, InMemoryRepositoryOptionsBuilder<TEntity>>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes the context operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InMemoryRepositoryOptionsBuilder<TEntity> Context(InMemoryContext<TEntity> context)
    {
        this.Target.Context = context;

        return this;
    }

    /// <summary>
    /// Executes the mapper operation.
    /// </summary>
    /// <param name="mapper">The mapper used to transform values.</param>
    /// <returns>The result of the operation.</returns>
    public InMemoryRepositoryOptionsBuilder<TEntity> Mapper(IEntityMapper mapper)
    {
        this.Target.Mapper = mapper;

        return this;
    }

    /// <summary>
    /// Publishes events.
    /// </summary>
    /// <param name="publishEvents">The publish events used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public InMemoryRepositoryOptionsBuilder<TEntity> PublishEvents(bool publishEvents)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    /// <summary>
    /// Executes the id generator operation.
    /// </summary>
    /// <param name="idGenerator">The id generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InMemoryRepositoryOptionsBuilder<TEntity> IdGenerator(IEntityIdGenerator<TEntity> idGenerator)
    {
        this.Target.IdGenerator = idGenerator;

        return this;
    }

    /// <summary>
    /// Executes the enable optimistic concurrency operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public InMemoryRepositoryOptionsBuilder<TEntity> EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }

    /// <summary>
    /// Executes the version generator operation.
    /// </summary>
    /// <param name="generator">The generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InMemoryRepositoryOptionsBuilder<TEntity> VersionGenerator(Func<Guid> generator)
    {
        this.Target.VersionGenerator = generator;

        return this;
    }
}
