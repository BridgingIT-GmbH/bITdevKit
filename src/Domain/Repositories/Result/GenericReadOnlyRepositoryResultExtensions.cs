// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Data;
using System.Data.Common;
using System.Net.Sockets;

/// <summary>
/// Provides extension methods for performing result-based queries on repositories.
/// </summary>
public static class GenericReadOnlyRepositoryResultExtensions
{
    /// <summary>
    /// Asynchronously counts the total number of entities and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the count of entities.</returns>
    public static async Task<Result<long>> CountResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var count = await source.CountAsync(cancellationToken).AnyContext();

            return Result<long>.Success(count);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously counts the number of entities based on the given specification and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="expression">The expression to filter the entity.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the count of entities that match the specification.</returns>
    public static async Task<Result<long>> CountResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.CountResultAsync(new Specification<TEntity>(expression), cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously counts the number of entities that satisfy the specified criteria and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="specification">The criteria that entities must satisfy to be counted.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the count of entities that satisfy the specified criteria.</returns>
    public static async Task<Result<long>> CountResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var count = await source.CountAsync(specification, cancellationToken).AnyContext();

            return Result<long>.Success(count);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously counts the number of entities that satisfy the provided specifications and returns the result as a Result object.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the count of entities that satisfy the provided specifications.</returns>
    public static async Task<Result<long>> CountResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var count = await source.CountAsync(specifications, cancellationToken).AnyContext();

            return Result<long>.Success(count);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// Asynchronously finds a single entity by its identifier and returns a result object.
    /// <param name="source">The repository source from which to find the entity.</param>
    /// <param name="id">The identifier of the entity to be found.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <typeparam name="TEntity">The type of the entity to be found.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains a result object with the found entity or a failure result if the entity was not found.</returns>
    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var entity = await source.FindOneAsync(id, options, cancellationToken).AnyContext();

            return entity is null ? Result<TEntity>.Failure<EntityNotFoundError>() : Result<TEntity>.Success(entity);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously finds a single entity based on the provided expression.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository from which to retrieve the entity.</param>
    /// <param name="expression">The expression to filter the entity.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that represents the asynchronous find operation. The task result contains the find result.
    /// </returns>
    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.FindOneResultAsync(new Specification<TEntity>(expression), options, cancellationToken).AnyContext();
    }

    /// Asynchronously finds and returns a single entity that matches the given specification.
    /// <param name="source">The repository from which the entity is to be fetched.</param>
    /// <param name="specification">The specification that defines the criteria for selecting the entity.</param>
    /// <param name="options">Optional settings for the entity retrieval process.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <typeparam name="TEntity">The type of the entity to be fetched.</typeparam>
    /// <returns>A result object containing the fetched entity if found, or a failure result if not found.</returns>
    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var entity = await source.FindOneAsync(specification, options, cancellationToken).AnyContext();

            return entity is null ? Result<TEntity>.Failure<EntityNotFoundError>() : Result<TEntity>.Success(entity);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously retrieves a single entity based on multiple specifications.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository to fetch the entity from.</param>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options for retrieval.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A result containing the found entity if successful, or an error if not found.</returns>
    public static async Task<Result<TEntity>> FindOneResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var entity = await source.FindOneAsync(specifications, options, cancellationToken).AnyContext();

            return entity is null ? Result<TEntity>.Failure<EntityNotFoundError>() : Result<TEntity>.Success(entity);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously retrieves all entities from the repository and returns them wrapped in a success result.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The generic read-only repository from which to retrieve entities.</param>
    /// <param name="options">Optional find options for the query.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a success result wrapping the retrieved entities.</returns>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<IEnumerable<TEntity>>.Success(
                await source.FindAllAsync(options, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TEntity>>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously finds all entities that match the given expression.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="expression">The expression used to filter entities.</param>
    /// <param name="options">Optional find options.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a result with the collection of found entities.</returns>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.FindAllResultAsync(new Specification<TEntity>(expression), options, cancellationToken).AnyContext();
    }

    public static async Task<PagedResult<TEntity>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        FilterModel filterModel,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            filterModel ??= new FilterModel();
            specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
            var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

            var count = await source.CountAsync(specifications, cancellationToken).AnyContext();
            var entities = await source.FindAllAsync(
                    specifications,
                    findOptions,
                    cancellationToken)
                .AnyContext();

            return PagedResult<TEntity>.Success(entities, count, filterModel.Page, filterModel.PageSize);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously finds all entities of type <typeparamref name="TEntity"/> that satisfy the specified
    /// <paramref name="specification"/> and returns them as a <see cref="Result{TValue}"/> of <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository from which to read the entities.</param>
    /// <param name="specification">The specification defining the conditions that the entities must satisfy.</param>
    /// <param name="options">Optional find options that may affect the query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="Result{TValue}"/>
    /// of <see cref="IEnumerable{T}"/> of <typeparamref name="TEntity"/>.</returns>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<IEnumerable<TEntity>>.Success(
                await source.FindAllAsync(specification, options, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TEntity>>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Finds all entities that match the given specifications and returns the result asynchronously.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="options">Optional find options for the query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the entities found.</returns>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<IEnumerable<TEntity>>.Success(
                await source.FindAllAsync(specifications, options, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TEntity>>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Finds all entity IDs in the repository based on the given options and cancellation token.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <typeparam name="TId">The type of the identifier.</typeparam>
    /// <param name="source">The repository source.</param>
    /// <param name="options">The options to be used for the find operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a Result object with the collection of IDs.</returns>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<IEnumerable<TId>>.Success(
                await source.FindAllIdsAsync<TEntity, TId>(options, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TId>>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously retrieves a list of entity identifiers that match the specified criteria.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the identifier.</typeparam>
    /// <param name="source">The generic read-only repository to extend.</param>
    /// <param name="expression">The expression used to filter the entities.</param>
    /// <param name="options">Optional additional options for the find operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a result object with a list of entity identifiers.</returns>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.FindAllIdsResultAsync<TEntity, TId>(new Specification<TEntity>(expression), options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously finds all IDs of entities that match a given specification.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the entity's ID.</typeparam>
    /// <param name="source">The source repository to query.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional parameters to customize the query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains a result object with a collection of entity IDs.</returns>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<IEnumerable<TId>>.Success(
                await source.FindAllIdsAsync<TEntity, TId>(specification, options, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TId>>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously finds all identifiers matching the given specifications and returns the result.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the identifier.</typeparam>
    /// <param name="source">The source repository from which to retrieve the entities.</param>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{TValue}"/> object with the resulting identifiers.</returns>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsResultAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            return Result<IEnumerable<TId>>.Success(
                await source.FindAllIdsAsync<TEntity, TId>(specifications, options, cancellationToken).AnyContext());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TId>>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Finds and returns a paged result of all entities from the repository.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="ordering">The ordering criteria.</param>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="includeExpression">The expression for including related entities.</param>
    /// <param name="includePath">The path for including related entities.</param>
    /// <param name="cancellationToken">A token to notify if the operation should be canceled.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paged result of entities.</returns>
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
        try
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
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously retrieves a paginated collection of entities from the repository.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <param name="source">The source repository from which the entities are retrieved.</param>
    /// <param name="orderingExpression">Expression used to order the entities.</param>
    /// <param name="page">The page number to retrieve (defaults to 1).</param>
    /// <param name="pageSize">The number of entities per page (defaults to 10).</param>
    /// <param name="orderDirection">The direction to order the entities (ascending or descending).</param>
    /// <param name="includeExpression">Optional expression to include related entities.</param>
    /// <param name="includePath">Optional include path to related entities.</param>
    /// <param name="cancellationToken">Optional token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="PagedResult{TEntity}"/> which includes the entities and pagination info.</returns>
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
        try
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
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously retrieves a paginated result set of entities based on a specified condition.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The generic read-only repository source.</param>
    /// <param name="expression">The expression to filter the entities.</param>
    /// <param name="ordering">The ordering criteria as a string.</param>
    /// <param name="page">The page number to retrieve. Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="includeExpression">The expression to include related entities.</param>
    /// <param name="includePath">The path to include related entities.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paged result set of entities.</returns>
    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        string ordering, // of the form >   fieldname [ascending|descending], ...
        int page = 1,
        int pageSize = 10,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.FindAllPagedResultAsync(
            new Specification<TEntity>(expression),
            ordering,
            page,
            pageSize,
            includeExpression,
            includePath,
            cancellationToken).AnyContext();
    }

    /// <summary>
    /// Retrieves a paginated result set of entities that match the provided specification.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository to query against.</param>
    /// <param name="specification">The specification to filter the entities.</param>
    /// <param name="ordering">The ordering criteria as a string.</param>
    /// <param name="page">The page number to retrieve. Default is 1.</param>
    /// <param name="pageSize">The number of entities per page. Default is 10.</param>
    /// <param name="includeExpression">Optional expression specifying related entities to include.</param>
    /// <param name="includePath">Optional path string specifying related entities to include.</param>
    /// <param name="cancellationToken">Optional cancellation token for the async operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paginated result set of entities.</returns>
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
        try
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
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Retrieves a paged result of entities based on the provided criteria.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository source to query.</param>
    /// <param name="expression">The filter expression to apply to the query.</param>
    /// <param name="orderingExpression">The expression to order the query results.</param>
    /// <param name="page">The page number to retrieve. Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="orderDirection">The direction of ordering (ascending or descending). Defaults to ascending.</param>
    /// <param name="includeExpression">An optional expression to include related entities.</param>
    /// <param name="includePath">An optional path to include related entities.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operations.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paged result of entities.</returns>
    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        Expression<Func<TEntity, object>> orderingExpression,
        int page = 1,
        int pageSize = 10,
        OrderDirection orderDirection = OrderDirection.Ascending,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.FindAllPagedResultAsync(
            new Specification<TEntity>(expression),
            orderingExpression,
            page,
            pageSize,
            orderDirection,
            includeExpression,
            includePath,
            cancellationToken).AnyContext();
    }

    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        FilterModel filterModel,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            filterModel ??= new FilterModel();
            specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
            var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

            var count = await source.CountAsync(specifications, cancellationToken).AnyContext();
            var entities = await source.FindAllAsync(
                    specifications,
                    findOptions,
                    cancellationToken)
                .AnyContext();

            return PagedResult<TEntity>.Success(entities, count, filterModel.Page, filterModel.PageSize);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Asynchronously retrieves a paged result of entities from the repository based on the provided specifications.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository instance.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="orderingExpression">The expression used for ordering the results.</param>
    /// <param name="page">The page number to retrieve (default is 1).</param>
    /// <param name="pageSize">The number of items per page (default is 10).</param>
    /// <param name="orderDirection">The direction to order the results (default is ascending).</param>
    /// <param name="includeExpression">The expression to include related entities (default is null).</param>
    /// <param name="includePath">The path to include related entities (default is null).</param>
    /// <param name="cancellationToken">Cancellation token (default is none).</param>
    /// <returns>A task representing the asynchronous operation, with a result of the paged entities.</returns>
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
        try
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
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Retrieves a paged result set of entities that satisfy the given specifications,
    /// with optional ordering, paging, and inclusion of related data.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The repository to retrieve data from.</param>
    /// <param name="specifications">The collection of specifications entities must satisfy.</param>
    /// <param name="ordering">The ordering clause for sorting the results.</param>
    /// <param name="page">The page number to retrieve. Default is 1.</param>
    /// <param name="pageSize">The number of items per page. Default is 10.</param>
    /// <param name="includeExpression">An optional expression to include related data.</param>
    /// <param name="includePath">An optional path to include related data.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paged result set of entities.</returns>
    public static async Task<PagedResult<TEntity>> FindAllPagedResultAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications, // filters
        string ordering, // of the form >   fieldname [ascending|descending], ...
        int page = 1,
        int pageSize = 10,
        Expression<Func<TEntity, object>> includeExpression = null,
        string includePath = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        try
        {
            var count = await source.CountAsync(specifications, cancellationToken).AnyContext();
            var entities = await source.FindAllAsync(
                    specifications,
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
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Retrieves a paged result of entities from the repository based on specified criteria and ordering.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="specifications">The specifications to apply to filter the entities.</param>
    /// <param name="orderingExpression">The expression to order the entities.</param>
    /// <param name="page">The page number to retrieve, starting from 1.</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <param name="orderDirection">The direction of the order (Ascending or Descending).</param>
    /// <param name="includeExpression">An optional expression to include related entities.</param>
    /// <param name="includePath">An optional path string to include related entities.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paged result of entities.</returns>
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
        try
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
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return PagedResult<TEntity>.Failure(ex.Message, new ExceptionError(ex));
        }
    }
}