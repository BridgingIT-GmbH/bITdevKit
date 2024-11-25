// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public class EntityFrameworkRepositoryWrapper<TEntity, TContext>(ILoggerFactory loggerFactory, TContext context)
    : EntityFrameworkGenericRepository<TEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : DbContext { }

public partial class EntityFrameworkGenericRepository<TEntity>
    : // TODO: rename to EntityFrameworkRepository + Obsolete
        EntityFrameworkReadOnlyGenericRepository<TEntity>, IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    protected EntityFrameworkGenericRepository(EntityFrameworkRepositoryOptions options)
        : base(options) { }

    public EntityFrameworkGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    public EntityFrameworkGenericRepository(ILoggerFactory loggerFactory, DbContext context)
        : base(o => o.LoggerFactory(loggerFactory).DbContext(context)) { }

    /// <summary>
    ///     Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken"></param>
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    ///     Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken"></param>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    ///     Insert or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    /// <param name="cancellationToken"></param>
    public virtual async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return (null, RepositoryActionResult.None);
        }

        var isNew = entity.Id == default;
        var existingEntity = isNew
            ? null
            : await this.FindOneAsync(entity.Id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext(); // prevent the entity from being tracked (which find() does
        isNew = isNew || existingEntity is null;

        if (isNew)
        {
            HandleInsert();
        }
        else
        {
            HandleUpdate();
        }

        if (this.Options.Autosave)
        {
            foreach (var entry in this.Options.DbContext.ChangeTracker.Entries())
            {
                TypedLogger.LogEntityState(this.Logger, Constants.LogKey, entry.Entity.GetType().Name, entry.IsKeySet, entry.State);
            }

            await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
        }

        return isNew ? (entity, RepositoryActionResult.Inserted) : (entity, RepositoryActionResult.Updated);

        void HandleInsert()
        {
            TypedLogger.LogUpsert(this.Logger, Constants.LogKey, "insert", typeof(TEntity).Name, entity.Id, false);

            if (entity is IConcurrency concurrencyEntity) // Set initial version before attaching
            {
                concurrencyEntity.Version = this.Options.VersionGenerator();
            }

            this.Options.DbContext.Set<TEntity>().Add(entity);
        }

        void HandleUpdate()
        {
            var isTracked = this.Options.DbContext.ChangeTracker.Entries<TEntity>().Any(e => e.Entity.Id.Equals(entity.Id));
            TypedLogger.LogUpsert(this.Logger, Constants.LogKey, "update", typeof(TEntity).Name, entity.Id, isTracked);

            if (entity is IConcurrency concurrentEntity)
            {
                var originalVersion = concurrentEntity.Version;
                concurrentEntity.Version = this.Options.VersionGenerator();

                if (isTracked)
                {
                    // For tracked entities, get the entry and set original version
                    this.Options.DbContext.Entry(entity).Property(nameof(IConcurrency.Version)).OriginalValue = originalVersion;
                }
                else
                {
                    // For untracked entities, attach and set original version
                    this.Options.DbContext.Update(entity); // right way to update disconnected entities https://docs.microsoft.com/en-us/ef/core/saving/disconnected-entities#working-with-graphs
                    this.Options.DbContext.Entry(entity).Property(nameof(IConcurrency.Version)).OriginalValue = originalVersion;
                }
            }
            else if (!isTracked)
            {
                this.Options.DbContext.Update(entity); // right way to update disconnected entities https://docs.microsoft.com/en-us/ef/core/saving/disconnected-entities#working-with-graphs
            }
        }
    }

    public virtual async Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var existingEntity = await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();
        if (existingEntity is not null)
        {
            this.Options.DbContext.Remove(existingEntity);

            if (this.Options.Autosave)
            {
                await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
            }

            return RepositoryActionResult.Deleted;
        }

        return RepositoryActionResult.None;
    }

    public virtual async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null || entity.Id == default)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(entity.Id, cancellationToken).AnyContext();
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} repository: upsert - {EntityUpsertType} (type={EntityType}, id={EntityId}, tracked={EntityTracked})")]
        public static partial void LogUpsert(ILogger logger, string logKey, string entityUpsertType, string entityType, object entityId, bool entityTracked);

        [LoggerMessage(2, LogLevel.Trace, "{LogKey} dbcontext entity state: {EntityType} (keySet={EntityKeySet}) -> {EntityEntryState}")]
        public static partial void LogEntityState(ILogger logger, string logKey, string entityType, bool entityKeySet, EntityState entityEntryState);
    }
}