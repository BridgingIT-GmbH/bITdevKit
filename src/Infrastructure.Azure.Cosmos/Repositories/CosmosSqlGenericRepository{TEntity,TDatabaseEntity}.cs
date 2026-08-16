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

/// <summary>
/// Represents cosmos sql repository wrapper.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TProvider">The provider type.</typeparam>
/// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
/// <param name="provider">The provider used by the operation.</param>
/// <param name="mapper">The mapper used to transform values.</param>
public class CosmosSqlRepositoryWrapper<TEntity, TProvider, TDatabaseEntity>(
    ILoggerFactory loggerFactory,
    TProvider provider,
    IEntityMapper mapper) : CosmosSqlGenericRepository<TEntity, TDatabaseEntity>(loggerFactory, provider, mapper)
    where TEntity : class, IEntity
    where TProvider : ICosmosSqlProvider<TDatabaseEntity>
    where TDatabaseEntity : class
{ }

/// <summary>
/// Represents cosmos sql generic repository.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDatabaseEntity">The database entity type.</typeparam>
public class CosmosSqlGenericRepository<TEntity, TDatabaseEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    /// <summary>
    /// Initializes a new instance of the <c>CosmosSqlGenericRepository</c> class.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    public CosmosSqlGenericRepository(CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity> options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Provider, nameof(options.Provider));
        EnsureArg.IsNotNull(options.Mapper, nameof(options.Mapper));

        this.Options = options;
        this.Options.IdGenerator ??= new EntityGuidIdGenerator<TEntity>();
        this.Logger = options.CreateLogger<CosmosSqlGenericRepository<TEntity, TDatabaseEntity>>();
        this.Provider = options.Provider;
    }

    /// <summary>
    /// Initializes a new instance of the <c>CosmosSqlGenericRepository</c> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public CosmosSqlGenericRepository(
        Builder<CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity>,
            CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity>> optionsBuilder)
        : this(optionsBuilder(new CosmosSqlRepositoryOptionsBuilder<TEntity, TDatabaseEntity>()).Build()) { }

    /// <summary>
    /// Initializes a new instance of the <c>CosmosSqlGenericRepository</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="provider">The provider used by the operation.</param>
    /// <param name="mapper">The mapper used to transform values.</param>
    public CosmosSqlGenericRepository(
        ILoggerFactory loggerFactory,
        ICosmosSqlProvider<TDatabaseEntity> provider,
        IEntityMapper mapper)
        : this(o => o.LoggerFactory(loggerFactory).Provider(provider).Mapper(mapper)) { }

    /// <summary>
    /// Gets the options.
    /// </summary>
    protected CosmosSqlRepositoryOptions<TEntity, TDatabaseEntity> Options { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger<CosmosSqlGenericRepository<TEntity, TDatabaseEntity>> Logger { get; }

    /// <summary>
    /// Gets the provider.
    /// </summary>
    protected ICosmosSqlProvider<TDatabaseEntity> Provider { get; }

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
    /// Finds all.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s)
                .Expand()); // expand fixes Invoke in expression issue
        var order = (options?.Orders ?? new List<OrderOption<TEntity>>()).Insert(options?.Order)
            .FirstOrDefault(); // cosmos only supports single orderby

        if (options?.Distinct is not null)
        {
            throw new NotSupportedException("Distinct is not supported for Cosmos");
        }

        var result = await this.Provider.ReadItemsAsync(expressions,
                options?.Skip ?? -1,
                options?.Take ?? -1,
                this.Options.Mapper.MapExpression<Expression<Func<TDatabaseEntity, object>>>(order?.Expression),
                order?.Direction == OrderDirection.Descending,
                cancellationToken: cancellationToken)
            .AnyContext();

        return result.Select(d => this.Options.Mapper.Map<TEntity>(d));
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return default;
        }

        return this.Options.Mapper.Map<TEntity>(await this.Provider
            .ReadItemAsync(id as string, cancellationToken: cancellationToken)
            .AnyContext());
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s)
                .Expand()); // expand fixes Invoke in expression issue

        var entities = await this.Options.Provider
            .ReadItemsAsync(expressions, -1, 1, cancellationToken: cancellationToken)
            .AnyContext();

        return this.Options.Mapper.Map<TEntity>(entities.FirstOrDefault());
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
            return (default, RepositoryActionResult.None);
        }

        var isNew = this.Options.IdGenerator.IsNew(entity.Id) ||
            !await this.ExistsAsync(entity.Id, cancellationToken).AnyContext();
        if (isNew)
        {
            this.Options.IdGenerator.SetNew(entity); // cosmos v3 needs an id, also for new documents
        }

        var result = this.Options.Mapper.Map<TEntity>(await this.Provider
            .UpsertItemAsync(this.Options.Mapper.Map<TDatabaseEntity>(entity), cancellationToken: cancellationToken)
            .AnyContext());

        return isNew ? (result, RepositoryActionResult.Inserted) : (result, RepositoryActionResult.Updated);
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

        var entity = await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();
        if (entity is not null)
        {
            return await this.DeleteAsync(entity, cancellationToken).AnyContext();
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
        if (entity?.Id == default)
        {
            return RepositoryActionResult.None;
        }

        var response = await this.Provider.DeleteItemAsync(entity.Id as string, cancellationToken: cancellationToken)
            .AnyContext();
        if (response)
        {
            return RepositoryActionResult.Deleted;
        }

        return RepositoryActionResult.None;
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

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s)
                .Expand()); // expand fixes Invoke in expression issue
        var result = await this.Provider.ReadItemsAsync(expressions, cancellationToken: cancellationToken).AnyContext();

        return result.LongCount();
    }
}
