// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class EntityFrameworkRepositoryWrapper<TEntity, TContext> : EntityFrameworkGenericRepository<TEntity>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    public EntityFrameworkRepositoryWrapper(ILoggerFactory loggerFactory, TContext context)
        : base(loggerFactory, context)
    {
    }
}

public class EntityFrameworkGenericRepository<TEntity> : // TODO: rename to EntityFrameworkRepository + Obsolete
    EntityFrameworkReadOnlyGenericRepository<TEntity>, IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public EntityFrameworkGenericRepository(EntityFrameworkRepositoryOptions options)
        : base(options)
    {
    }

    public EntityFrameworkGenericRepository(Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build())
    {
    }

    public EntityFrameworkGenericRepository(ILoggerFactory loggerFactory, DbContext context)
        : base(o => o.LoggerFactory(loggerFactory).DbContext(context))
    {
    }

    /// <summary>
    /// Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    public virtual async Task<TEntity> InsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();
        return result.entity;
    }

    /// <summary>
    /// Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public virtual async Task<TEntity> UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();
        return result.entity;
    }

    /// <summary>
    /// Insert or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
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
            this.Logger.LogDebug("{LogKey} repository: upsert - insert (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);
            this.Options.DbContext.Set<TEntity>().Add(entity);
        }
        else
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - update (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);
            if (!this.Options.DbContext.ChangeTracker.Entries<TEntity>().Any(e => e.Entity.Id.Equals(entity.Id)))
            {
                // only re-attach (+update) if not tracked already
                this.Options.DbContext.Update(entity); // right way to update disconnected entities https://docs.microsoft.com/en-us/ef/core/saving/disconnected-entities#working-with-graphs
            }
        }

        if (this.Options.Autosave)
        {
            foreach (var entry in this.Options.DbContext.ChangeTracker.Entries())
            {
                this.Logger.LogDebug("{LogKey} dbcontext entity state: {entityType} (keySet={entityKeySet}) -> {entryState}", Constants.LogKey, entry.Entity.GetType().Name, entry.IsKeySet, entry.State);
            }

            await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
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
}