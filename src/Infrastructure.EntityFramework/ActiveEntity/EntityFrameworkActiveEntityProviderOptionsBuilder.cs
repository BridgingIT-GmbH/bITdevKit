// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using static BridgingIT.DevKit.Infrastructure.EntityFramework.GenericEntityMergeStrategy;

/// <summary>
/// Builds entity framework active entity provider options configuration.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>
    : OptionsBuilderBase<EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>, EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    /// <summary>
    /// Publishes events.
    /// </summary>
    /// <param name="publishEvents">The publish events used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> PublishEvents(bool publishEvents = true)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    /// <summary>
    /// Executes the enable optimistic concurrency operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }

    /// <summary>
    /// Executes the version generator operation.
    /// </summary>
    /// <param name="generator">The generator used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> VersionGenerator(Func<Guid> generator)
    {
        this.Target.VersionGenerator = generator;

        return this;
    }

    /// <summary>
    /// Executes the options) operation.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <returns>The result of the operation.</returns>
    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> GenericMergeStrategy(Options options = null)
    {
        this.Target.MergeStrategy = (ctx, entity, ct) =>
            MergeAsync(ctx, entity, options ?? new(), ct);

        return this;
    }

    /// <summary>
    /// Executes the merge strategy operation.
    /// </summary>
    /// <param name="strategy">The strategy used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> MergeStrategy(
        Func<TContext, TEntity, CancellationToken, Task<TEntity>> strategy)
    {
        this.Target.MergeStrategy = strategy;

        return this;
    }
}
