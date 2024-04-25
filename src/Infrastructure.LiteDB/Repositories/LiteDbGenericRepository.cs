// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;

public class LiteDbGenericRepository<TEntity> :
    LiteDbReadOnlyGenericRepository<TEntity>, IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public LiteDbGenericRepository(ILiteDbRepositoryOptions options)
        : base(options)
    {
    }

    public LiteDbGenericRepository(Builder<LiteDbRepositoryOptionsBuilder, LiteDbRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new LiteDbRepositoryOptionsBuilder()).Build())
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
            : await this.FindOneAsync(entity.Id, null, cancellationToken).AnyContext(); // prevent the entity from being tracked (which find() does
        isNew = isNew || existingEntity is null;

        if (isNew)
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - insert (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);
            this.Options.DbContext.Database.GetCollection<TEntity>().Insert(entity);
        }
        else
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - update (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);
            this.Options.DbContext.Database.GetCollection<TEntity>().Update(entity);
        }

        //if (this.Options.Autosave)
        //{
        //}

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
            this.Options.DbContext.Database.GetCollection<TEntity>().DeleteMany(e => e.Id == existingEntity.Id);

            //if (this.Options.Autosave)
            //{
            //}

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