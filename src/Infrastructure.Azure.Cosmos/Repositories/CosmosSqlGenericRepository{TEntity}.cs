// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;
using Common;
using Domain.Model;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class CosmosSqlRepositoryWrapper<TEntity, TProvider>(ILoggerFactory loggerFactory, TProvider provider)
    : CosmosSqlGenericRepository<TEntity>(loggerFactory, provider)
    where TEntity : class, IEntity
    where TProvider : ICosmosSqlProvider<TEntity> { }

public class CosmosSqlGenericRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public CosmosSqlGenericRepository(CosmosSqlGenericRepositoryOptions<TEntity> options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Provider, nameof(options.Provider));

        this.Options = options;
        this.Options.IdGenerator ??= new EntityGuidIdGenerator<TEntity>();
        this.Logger = options.LoggerFactory?.CreateLogger<CosmosSqlGenericRepository<TEntity>>() ??
            NullLoggerFactory.Instance.CreateLogger<CosmosSqlGenericRepository<TEntity>>();
        this.Provider = options.Provider;
    }

    public CosmosSqlGenericRepository(
        Builder<CosmosSqlGenericRepositoryOptionsBuilder<TEntity>, CosmosSqlGenericRepositoryOptions<TEntity>>
            optionsBuilder)
        : this(optionsBuilder(new CosmosSqlGenericRepositoryOptionsBuilder<TEntity>()).Build()) { }

    public CosmosSqlGenericRepository(ILoggerFactory loggerFactory, ICosmosSqlProvider<TEntity> provider)
        : this(o => o.LoggerFactory(loggerFactory).Provider(provider)) { }

    protected CosmosSqlGenericRepositoryOptions<TEntity> Options { get; }

    protected ILogger<CosmosSqlGenericRepository<TEntity>> Logger { get; }

    protected ICosmosSqlProvider<TEntity> Provider { get; }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => s.ToExpression().Expand()); // expand fixes Invoke in expression issue
        var order = (options?.Orders ?? new List<OrderOption<TEntity>>()).Insert(options?.Order)
            .FirstOrDefault(); // cosmos only supports single orderby

        if (options?.Distinct is not null)
        {
            throw new NotSupportedException("Distinct is not supported for Cosmos");
        }

        return (await this.Provider.ReadItemsAsync(expressions,
                options?.Skip ?? -1,
                options?.Take ?? -1,
                order?.Expression,
                order?.Direction == OrderDirection.Descending,
                cancellationToken: cancellationToken)
            .AnyContext()).ToList();
    }

    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return default;
        }

        return await this.Provider.ReadItemAsync(id.ToString(), cancellationToken: cancellationToken).AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync([specification], options, cancellationToken).AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => s.ToExpression().Expand()); // expand fixes Invoke in expression issue

        return (await this.Options.Provider.ReadItemsAsync(expressions, -1, 1, cancellationToken: cancellationToken)
            .AnyContext()).FirstOrDefault();
    }

    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        return await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext() is not null;
    }

    /// <summary>
    ///     Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return (await this.UpsertAsync(entity, cancellationToken).AnyContext()).entity;
    }

    /// <summary>
    ///     Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return (await this.UpsertAsync(entity, cancellationToken).AnyContext()).entity;
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
            return (default, RepositoryActionResult.None);
        }

        var isNew = this.Options.IdGenerator.IsNew(entity.Id) ||
            !await this.ExistsAsync(entity.Id, cancellationToken).AnyContext();
        if (isNew)
        {
            this.Options.IdGenerator.SetNew(entity); // cosmos v3 needs an id, also for new items
        }

        var result = await this.Provider.UpsertItemAsync(entity, cancellationToken: cancellationToken).AnyContext();

        return isNew ? (result, RepositoryActionResult.Inserted) : (result, RepositoryActionResult.Updated);
    }

    public virtual async Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var entity = await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();
        if (entity is not null)
        {
            return await this.DeleteAsync(entity, cancellationToken).AnyContext();
        }

        return RepositoryActionResult.None;
    }

    public virtual async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity?.Id == default)
        {
            return RepositoryActionResult.None;
        }

        var response = await this.Provider.DeleteItemAsync(entity.Id.ToString(), cancellationToken: cancellationToken)
            .AnyContext();
        if (response)
        {
            return RepositoryActionResult.Deleted;
        }

        return RepositoryActionResult.None;
    }

    public virtual async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => s.ToExpression().Expand()); // expand fixes Invoke in expression issue

        return (await this.Provider.ReadItemsAsync(expressions, cancellationToken: cancellationToken).AnyContext())
            .LongCount();
    }
}