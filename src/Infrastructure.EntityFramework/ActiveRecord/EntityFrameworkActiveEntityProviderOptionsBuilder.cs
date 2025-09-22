// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using static BridgingIT.DevKit.Infrastructure.EntityFramework.GenericEntityMergeStrategy;

public class EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>
    : OptionsBuilderBase<EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>, EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> PublishEvents(bool publishEvents = true)
    {
        this.Target.PublishEvents = publishEvents;

        return this;
    }

    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> EnableOptimisticConcurrency(bool value = true)
    {
        this.Target.EnableOptimisticConcurrency = value;

        return this;
    }

    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> VersionGenerator(Func<Guid> generator)
    {
        this.Target.VersionGenerator = generator;

        return this;
    }

    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> GenericMergeStrategy(Options options = null)
    {
        this.Target.MergeStrategy = (ctx, entity, ct) =>
            GenericEntityMergeStrategy.MergeAsync(ctx, entity, options ?? new(), ct);

        return this;
    }

    public EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity> MergeStrategy(
        Func<TContext, TEntity, CancellationToken, Task<TEntity>> strategy)
    {
        this.Target.MergeStrategy = strategy;

        return this;
    }
}