// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System.Linq.Expressions;
using Common;
using Domain.Model;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents lite db read only generic repository.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class LiteDbReadOnlyGenericRepository<TEntity> : IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Initializes a new instance of the <c>LiteDbReadOnlyGenericRepository</c> class.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    public LiteDbReadOnlyGenericRepository(ILiteDbRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    /// <summary>
    /// Initializes a new instance of the <c>LiteDbReadOnlyGenericRepository</c> class.
    /// </summary>
    /// <param name="optionsBuilder">The options builder used by the operation.</param>
    public LiteDbReadOnlyGenericRepository(
        Builder<LiteDbRepositoryOptionsBuilder, LiteDbRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new LiteDbRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Gets the options.
    /// </summary>
    protected ILiteDbRepositoryOptions Options { get; }

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
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
                //.IncludeIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                //.DistinctIf(options)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToList());
        }

        return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
            //.IncludeIf(options)
            .WhereExpressions(expressions)
            //.DistinctIf(options)
            .SkipIf(options?.Skip)
            .TakeIf(options?.Take)
            .ToList());
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([], projection, options, cancellationToken).AnyContext();
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
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([specification], projection, options, cancellationToken).AnyContext();
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
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.SafeNull().ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
                //.IncludeIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                .Select(projection)
                //.DistinctIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToList());
        }

        return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
            //.IncludeIf(options)
            .WhereExpressions(expressions)
            .Select(projection)
            //.DistinctIf(options, this.Options.Mapper)
            .SkipIf(options?.Skip)
            .TakeIf(options?.Take)
            .ToList());
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
            return null;
        }

        return await this.FindOneAsync(new Specification<TEntity>(e => e.Id == this.ConvertEntityId(id)),
                options,
                cancellationToken)
            .AnyContext();
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
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.SafeNull().ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
            //.IncludeIf(options)
            .WhereExpressions(expressions)
            .FirstOrDefault());
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
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await Task.FromResult(this.Options.DbContext.Database.GetCollection<TEntity>()
            .WhereExpressions(expressions)
            .Count());
    }

    private object ConvertEntityId(object value)
    {
        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(Guid) &&
            value?.GetType() == typeof(string))
        {
            // string to guid conversion
            return Guid.Parse(value.ToString());
        }

        return value;
    }
}
