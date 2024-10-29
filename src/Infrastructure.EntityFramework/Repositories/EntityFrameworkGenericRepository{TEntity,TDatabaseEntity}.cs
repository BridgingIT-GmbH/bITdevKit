// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

public class EntityFrameworkRepositoryWrapper<TEntity, TDatabaseEntity, TContext>(
    ILoggerFactory loggerFactory,
    TContext context,
    IEntityMapper mapper) : EntityFrameworkGenericRepository<TEntity, TDatabaseEntity>(loggerFactory, context, mapper)
    where TEntity : class, IEntity
    where TDatabaseEntity : class
    where TContext : DbContext { }

public class
    EntityFrameworkGenericRepository<TEntity, TDatabaseEntity> // TODO: rename to EntityFrameworkRepository + Obsolete
    : EntityFrameworkReadOnlyGenericRepository<TEntity, TDatabaseEntity>, IGenericRepository<TEntity>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    public EntityFrameworkGenericRepository(EntityFrameworkRepositoryOptions options)
        : base(options) { }

    protected EntityFrameworkGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    protected EntityFrameworkGenericRepository(ILoggerFactory loggerFactory, DbContext context, IEntityMapper mapper)
        : base(o => o.LoggerFactory(loggerFactory).DbContext(context).Mapper(mapper)) { }

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
            : await this.Options.DbContext.Set<TDatabaseEntity>()
                .FindAsync([this.ConvertEntityId(entity.Id)], cancellationToken)
                .AnyContext(); // INFO: don't use this.FindOne here, existingEntity should be a TDatabaseEntity for the Remove to work
        isNew = isNew || existingEntity is null;

        if (isNew)
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - insert (type={entityType}, id={entityId})",
                Constants.LogKey,
                typeof(TEntity).Name,
                entity.Id);
            var dbEntity = this.Options.Mapper.Map<TDatabaseEntity>(entity);
            this.Options.DbContext.Set<TDatabaseEntity>().Add(dbEntity);

            if (this.Options.Autosave)
            {
                await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
                this.Options.Mapper.Map(dbEntity, entity); // map the db generated Id back to the (domain) entity.
            }
        }
        else
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - update (type={entityType}, id={entityId})",
                Constants.LogKey,
                typeof(TEntity).Name,
                entity.Id);
            this.Options.DbContext.Entry(existingEntity)
                .CurrentValues.SetValues(this.Options.Mapper.Map<TDatabaseEntity>(entity));

            if (this.Options.Autosave)
            {
                foreach (var entry in this.Options.DbContext.ChangeTracker.Entries())
                {
                    this.Logger.LogTrace(
                        "{LogKey} dbcontext entity state: {entityType} (keySet={entityKeySet}) -> {entryState}",
                        Constants.LogKey,
                        entry.Entity.GetType().Name,
                        entry.IsKeySet,
                        entry.State);
                }

                await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
            }
        }

        return isNew ? (entity, RepositoryActionResult.Inserted) : (entity, RepositoryActionResult.Updated);
    }

    public virtual async Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var existingEntity = await this.Options.DbContext.Set<TDatabaseEntity>()
            .FindAsync([this.ConvertEntityId(id) /*, cancellationToken: cancellationToken*/], cancellationToken)
            .AnyContext(); // INFO: don't use this.FindOne here, existingEntity should be a TDatabaseEntity for the Remove to work
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
        if (entity?.Id is null)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(entity.Id, cancellationToken).AnyContext();
    }
}