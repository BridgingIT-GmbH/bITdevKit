// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using Common;
using Model;
using Specifications;

public static class RepositoryResultExtensions
{
    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var entity = await source.FindOneAsync(id, options, cancellationToken).AnyContext();

        return entity is null ? Result<TEntity>.Failure<NotFoundResultError>() : Result<TEntity>.Success(entity);
    }

    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var entity = await source.FindOneAsync(specification, options, cancellationToken).AnyContext();

        return entity is null ? Result<TEntity>.Failure<NotFoundResultError>() : Result<TEntity>.Success(entity);
    }

    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var entity = await source.FindOneAsync(specifications, options, cancellationToken).AnyContext();

        return entity is null ? Result<TEntity>.Failure<NotFoundResultError>() : Result<TEntity>.Success(entity);
    }

    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return Result<IEnumerable<TEntity>>.Success(await source.FindAllAsync(options, cancellationToken).AnyContext());
    }

    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return Result<IEnumerable<TEntity>>.Success(await source.FindAllAsync(specification, options, cancellationToken)
            .AnyContext());
    }

    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return Result<IEnumerable<TEntity>>.Success(await source.FindAllAsync(specifications,
                options,
                cancellationToken)
            .AnyContext());
    }

    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return Result<IEnumerable<TId>>.Success(await source.FindAllIdsAsync<TEntity, TId>(options, cancellationToken)
            .AnyContext());
    }

    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return Result<IEnumerable<TId>>.Success(await source.FindAllIdsAsync<TEntity, TId>(specification,
                options,
                cancellationToken)
            .AnyContext());
    }

    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return Result<IEnumerable<TId>>.Success(await source.FindAllIdsAsync<TEntity, TId>(specifications,
                options,
                cancellationToken)
            .AnyContext());
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        string ordering, // of the form >   fieldname [ascending|descending], ...
        int page = 1,
        int pageSize = 10,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var count = await source.CountAsync(cancellationToken).AnyContext();

        var entities = await source.FindAllAsync(new FindOptions<TEntity>
                {
                    Order = !ordering.IsNullOrEmpty() ? new OrderOption<TEntity>(ordering) : null,
                    Skip = (page - 1) * pageSize,
                    Take = pageSize,
                    Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) :
                        !includePath.IsNullOrEmpty() ? new IncludeOption<TEntity>(includePath) : null
                },
                cancellationToken)
            .AnyContext();

        return PagedResult<TEntity>.Success(entities, count, page, pageSize);
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, object>> orderingExpression,
        int page = 1,
        int pageSize = 10,
        OrderDirection orderDirection = OrderDirection.Ascending,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var count = await source.CountAsync(cancellationToken).AnyContext();

        var entities = await source.FindAllAsync(new FindOptions<TEntity>
                {
                    Order = new OrderOption<TEntity>(orderingExpression, orderDirection),
                    Skip = (page - 1) * pageSize,
                    Take = pageSize,
                    Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) :
                        !includePath.IsNullOrEmpty() ? new IncludeOption<TEntity>(includePath) : null
                },
                cancellationToken)
            .AnyContext();

        return PagedResult<TEntity>.Success(entities, count, page, pageSize);
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        string ordering, // of the form >   fieldname [ascending|descending], ...
        int page = 1,
        int pageSize = 10,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var count = await source.CountAsync(specification, cancellationToken).AnyContext();

        var entities = await source.FindAllAsync(specification,
                new FindOptions<TEntity>
                {
                    Order = !ordering.IsNullOrEmpty() ? new OrderOption<TEntity>(ordering) : null,
                    Skip = (page - 1) * pageSize,
                    Take = pageSize,
                    Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) :
                        !includePath.IsNullOrEmpty() ? new IncludeOption<TEntity>(includePath) : null
                },
                cancellationToken)
            .AnyContext();

        return PagedResult<TEntity>.Success(entities, count, page, pageSize);
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, object>> orderingExpression,
        int page = 1,
        int pageSize = 10,
        OrderDirection orderDirection = OrderDirection.Ascending,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var count = await source.CountAsync(specification, cancellationToken).AnyContext();

        var entities = await source.FindAllAsync(specification,
                new FindOptions<TEntity>
                {
                    Order = new OrderOption<TEntity>(orderingExpression, orderDirection),
                    Skip = (page - 1) * pageSize,
                    Take = pageSize,
                    Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) :
                        !includePath.IsNullOrEmpty() ? new IncludeOption<TEntity>(includePath) : null
                },
                cancellationToken)
            .AnyContext();

        return PagedResult<TEntity>.Success(entities, count, page, pageSize);
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        string ordering, // of the form >   fieldname [ascending|descending], ...
        int page = 1,
        int pageSize = 10,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var count = await source.CountAsync(specifications, cancellationToken).AnyContext();

        var entities = await source.FindAllAsync(specifications,
                new FindOptions<TEntity>
                {
                    Order = !ordering.IsNullOrEmpty() ? new OrderOption<TEntity>(ordering) : null,
                    Skip = (page - 1) * pageSize,
                    Take = pageSize,
                    Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) :
                        !includePath.IsNullOrEmpty() ? new IncludeOption<TEntity>(includePath) : null
                },
                cancellationToken)
            .AnyContext();

        return PagedResult<TEntity>.Success(entities, count, page, pageSize);
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, object>> orderingExpression,
        int page = 1,
        int pageSize = 10,
        OrderDirection orderDirection = OrderDirection.Ascending,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var count = await source.CountAsync(specifications, cancellationToken).AnyContext();

        var entities = await source.FindAllAsync(specifications,
                new FindOptions<TEntity>
                {
                    Order = new OrderOption<TEntity>(orderingExpression, orderDirection),
                    Skip = (page - 1) * pageSize,
                    Take = pageSize,
                    Include = includeExpression is not null ? new IncludeOption<TEntity>(includeExpression) :
                        !includePath.IsNullOrEmpty() ? new IncludeOption<TEntity>(includePath) : null
                },
                cancellationToken)
            .AnyContext();

        return PagedResult<TEntity>.Success(entities, count, page, pageSize);
    }
}