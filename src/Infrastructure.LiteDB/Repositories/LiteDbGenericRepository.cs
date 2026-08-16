// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using Common;
using Domain;
using Domain.Model;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using Constants = Domain.Constants;

/// <summary>
/// Represents lite db generic repository.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="options">The options controlling the operation.</param>
public class LiteDbGenericRepository<TEntity>(ILiteDbRepositoryOptions options)
    : LiteDbReadOnlyGenericRepository<TEntity>(options), IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Initializes a new instance of the <c>LiteDbGenericRepository</c> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public LiteDbGenericRepository(Builder<LiteDbRepositoryOptionsBuilder, LiteDbRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new LiteDbRepositoryOptionsBuilder()).Build()) { }

    /// <inheritdoc />
    public virtual Task<long> UpdateSetAsync(
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public virtual Task<long> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public virtual Task<long> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> InsertSetAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var result = new List<TEntity>();

        foreach (var entity in entities.SafeNull())
        {
            result.Add(await this.InsertAsync(entity, cancellationToken).AnyContext());
        }

        return result.Where(e => e is not null);
    }

    /// <summary>
    ///     Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    ///     Insert or updates the provided entity.
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
            : await this.FindOneAsync(entity.Id, null, cancellationToken)
                .AnyContext(); // prevent the entity from being tracked (which find() does
        isNew = isNew || existingEntity is null;

        if (isNew)
        {
            this.Logger.LogDebug("[{LogKey}] repository: upsert - insert (type={entityType}, id={entityId})",
                Constants.LogKey,
                typeof(TEntity).Name,
                entity.Id);
            this.Options.DbContext.Database.GetCollection<TEntity>().Insert(entity);
        }
        else
        {
            this.Logger.LogDebug("[{LogKey}] repository: upsert - update (type={entityType}, id={entityId})",
                Constants.LogKey,
                typeof(TEntity).Name,
                entity.Id);
            this.Options.DbContext.Database.GetCollection<TEntity>().Update(entity);
        }

        //if (this.Options.Autosave)
        //{
        //}

        return isNew ? (entity, RepositoryActionResult.Inserted) : (entity, RepositoryActionResult.Updated);
    }

    /// <summary>
    /// Deletes .
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Deletes .
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <inheritdoc />
    public virtual Task<long> DeleteSetAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public virtual Task<long> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public virtual Task<long> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
