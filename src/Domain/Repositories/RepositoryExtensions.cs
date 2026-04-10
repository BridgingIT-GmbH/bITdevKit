// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Provides extension methods for the <see cref="IGenericReadOnlyRepository{TEntity}" /> interface.
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// Updates all entities matching the provided expression in a single set-based operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    /// <param name="source">The repository used to execute the bulk update.</param>
    /// <param name="expression">The expression used to build the filtering specification.</param>
    /// <param name="set">The builder action that defines which properties are updated and how their values are assigned.</param>
    /// <param name="options">Optional query options used to shape the update operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the number of affected entities.
    /// </returns>
    public static async Task<long> UpdateSetAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    where TEntity : class, IEntity
    {
        return await source.UpdateSetAsync(new Specification<TEntity>(expression), set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates all entities matched by the filter model and additional specifications in a single set-based operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    /// <param name="source">The repository used to execute the bulk update.</param>
    /// <param name="filterModel">The filter model that is translated into specifications and query options.</param>
    /// <param name="set">The builder action that defines which properties are updated and how their values are assigned.</param>
    /// <param name="specifications">Optional additional specifications that are combined with the specifications built from the filter model.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the number of affected entities.
    /// </returns>
    public static async Task<long> UpdateSetAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        FilterModel filterModel,
        Action<IEntityUpdateSet<TEntity>> set,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
    where TEntity : class, IEntity
    {
        filterModel ??= new FilterModel();
        specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
        var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

        return await source.UpdateSetAsync(specifications, set, findOptions, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes all entities matching the provided expression in a single set-based operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    /// <param name="source">The repository used to execute the bulk delete.</param>
    /// <param name="expression">The expression used to build the filtering specification.</param>
    /// <param name="options">Optional query options used to shape the delete operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the number of affected entities.
    /// </returns>
    public static async Task<long> DeleteSetAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    where TEntity : class, IEntity
    {
        return await source.DeleteSetAsync(new Specification<TEntity>(expression), options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes all entities matched by the filter model and additional specifications in a single set-based operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    /// <param name="source">The repository used to execute the bulk delete.</param>
    /// <param name="filterModel">The filter model that is translated into specifications and query options.</param>
    /// <param name="specifications">Optional additional specifications that are combined with the specifications built from the filter model.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the number of affected entities.
    /// </returns>
    public static async Task<long> DeleteSetAsync<TEntity>(
        this IGenericRepository<TEntity> source,
        FilterModel filterModel,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
    where TEntity : class, IEntity
    {
        filterModel ??= new FilterModel();
        specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
        var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

        return await source.DeleteSetAsync(specifications, findOptions, cancellationToken).AnyContext();
    }

    public static async Task<TEntity> FindOneAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    where TEntity : class, IEntity
    {
        return await source.FindOneAsync(new Specification<TEntity>(expression), options, cancellationToken).AnyContext();
    }

    public static async Task<TEntity> FindOneAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        FilterModel filterModel,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
    where TEntity : class, IEntity
    {
        filterModel ??= new FilterModel();
        specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
        var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

        return await source.FindOneAsync(specifications, findOptions, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously finds all entities that match the given expression.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="expression">The expression used to filter entities.</param>
    /// <param name="options">Optional find options.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a the collection of found entities.</returns>
    public static async Task<IEnumerable<TEntity>> FindAllAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.FindAllAsync(new Specification<TEntity>(expression), options, cancellationToken).AnyContext();
    }

    public static async Task<IEnumerable<TEntity>> FindAllAsync<TEntity>(
        this IGenericReadOnlyRepository<TEntity> source,
        FilterModel filterModel,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        filterModel ??= new FilterModel();
        specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
        var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

        return await source.FindAllAsync(specifications, findOptions, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously projects all entities of type <typeparamref name="TEntity" /> to a specified projection type based
    ///     on the given specification, projection expression, options, and cancellation token.
    /// </summary>
    /// <typeparam name="TProjection">The type to which the entities will be projected.</typeparam>
    /// <param name="expression">The expression used to filter entities.</param>
    /// <param name="projection">The expression defining the projection of entities.</param>
    /// <param name="options">Optional criteria for filtering and sorting results.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an enumerable collection of
    ///     projected entities of type <typeparamref name="TProjection" />.
    /// </returns>
    public static async Task<IEnumerable<TProjection>> ProjectAllAsync<TEntity, TProjection>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await source.ProjectAllAsync(
            new Specification<TEntity>(expression), projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities based on the filter model and specifications to the specified projection type.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TProjection">The type to project the entities to.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="filterModel">The filter model to apply.</param>
    /// <param name="projection">The expression defining the projection.</param>
    /// <param name="specifications">Optional additional specifications to apply.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the projected entities.</returns>
    public static async Task<IEnumerable<TProjection>> ProjectAllAsync<TEntity, TProjection>(
        this IGenericReadOnlyRepository<TEntity> source,
        FilterModel filterModel,
        Expression<Func<TEntity, TProjection>> projection,
        IEnumerable<ISpecification<TEntity>> specifications = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        filterModel ??= new FilterModel();
        specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
        var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

        return await source.ProjectAllAsync(specifications, projection, findOptions, cancellationToken).AnyContext();
    }

    /// <summary>
    ///     Asynchronously finds all IDs that match the given specifications.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the ID.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of IDs.</returns>
    public static async Task<IEnumerable<TId>> FindAllIdsAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(e => e.Id, options, cancellationToken).AnyContext())
            .Select(i => i.To<TId>());
    }

    /// <summary>
    ///     Asynchronously finds all IDs that match the given specification.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the ID.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="expression">The expression used to filter entities.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of IDs.</returns>
    public static async Task<IEnumerable<TId>> FindAllIdsAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(
                new Specification<TEntity>(expression), e => e.Id, options, cancellationToken).AnyContext())
            .Select(i => i.To<TId>());
    }

    public static async Task<IEnumerable<TId>> FindAllIdsAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        FilterModel filterModel,
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        filterModel ??= new FilterModel();
        specifications = SpecificationBuilder.Build(filterModel, specifications).ToArray();
        var findOptions = FindOptionsBuilder.Build<TEntity>(filterModel);

        return (await source.ProjectAllAsync(specifications, e => e.Id, findOptions, cancellationToken).AnyContext())
            .Select(i => i.To<TId>());
    }

    /// <summary>
    ///     Asynchronously finds all IDs that match the given specification.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the ID.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="specification">A specification to filter entities.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of IDs.</returns>
    public static async Task<IEnumerable<TId>> FindAllIdsAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(specification, e => e.Id, options, cancellationToken).AnyContext())
            .Select(i => i.To<TId>());
    }

    /// <summary>
    ///     Asynchronously finds all IDs that match the given specifications.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TId">The type of the ID.</typeparam>
    /// <param name="source">The source repository.</param>
    /// <param name="specifications">A collection of specifications to filter entities.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of IDs.</returns>
    public static async Task<IEnumerable<TId>> FindAllIdsAsync<TEntity, TId>(
        this IGenericReadOnlyRepository<TEntity> source,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return (await source.ProjectAllAsync(specifications, e => e.Id, options, cancellationToken).AnyContext())
            .Select(i => i.To<TId>());
    }
}
