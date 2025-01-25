// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
///     Represents the base interface for all repository abstractions.
///     Repositories serve as a bridge between the domain and data mapping layers, enabling
///     a more abstract and decoupled approach to data access.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
public interface IGenericReadOnlyRepository<TEntity> : IRepository
    where TEntity : class, IEntity
{
    /// <summary>
    ///     Asynchronously retrieves all entities of type <typeparamref name="TEntity" /> based on the specified find options
    ///     and cancellation token.
    /// </summary>
    /// <param name="options">An optional set of criteria to filter and sort the results.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an enumerable collection of
    ///     entities of type <typeparamref name="TEntity" />.
    /// </returns>
    Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds all entities that match the specified criteria asynchronously.
    /// </summary>
    /// <param name="specification">The specification to filter the entities.</param>
    /// <param name="options">Optional find options for querying the entities.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an enumerable of entities that
    ///     match the specification.
    /// </returns>
    Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds all entities that match the given specifications.
    /// </summary>
    /// <param name="specifications">The collection of specifications to filter the entities.</param>
    /// <param name="options">Optional find options to apply additional query customization.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a collection of entities that
    ///     match the given specifications.
    /// </returns>
    Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously finds a single entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="options">Optional query options for finding the entity.</param>
    /// <param name="cancellationToken">Optional token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A Task that represents the asynchronous operation. The task result contains the found entity,
    ///     or null if no entity with the given identifier is found.
    /// </returns>
    Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds a single entity that matches the specified criteria.
    /// </summary>
    /// <param name="specification">The specification that defines the criteria for the entity to be found.</param>
    /// <param name="options">Optional find options to customize the search behavior.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous find operation. The task result contains the found entity, or null if
    ///     no entity matches the criteria.
    /// </returns>
    Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously retrieves a single entity of type <typeparamref name="TEntity" /> based on the specified
    ///     specifications and find options.
    /// </summary>
    /// <param name="specifications">A collection of criteria used to filter the entities.</param>
    /// <param name="options">
    ///     Optional find options to modify query behavior, such as sorting. If null, default options are
    ///     used.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a single entity of type
    ///     <typeparamref name="TEntity" /> if found; otherwise, null.
    /// </returns>
    Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Projects all entities of type <typeparamref name="TEntity" /> to a collection of type
    ///     <typeparamref name="TProjection" />.
    /// </summary>
    /// <typeparam name="TProjection">The type to which entities should be projected.</typeparam>
    /// <param name="projection">
    ///     Expression defining the projection of <typeparamref name="TEntity" /> to
    ///     <typeparamref name="TProjection" />.
    /// </param>
    /// <param name="options">Optional parameters for customizing the find operation.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing the projected collection of type
    ///     <typeparamref name="TProjection" />.
    /// </returns>
    Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously projects all entities of type <typeparamref name="TEntity" /> to a specified projection type based
    ///     on the given specification, projection expression, options, and cancellation token.
    /// </summary>
    /// <typeparam name="TProjection">The type to which the entities will be projected.</typeparam>
    /// <param name="specification">The specification that entities must satisfy.</param>
    /// <param name="projection">The expression defining the projection of entities.</param>
    /// <param name="options">Optional criteria for filtering and sorting results.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an enumerable collection of
    ///     projected entities of type <typeparamref name="TProjection" />.
    /// </returns>
    Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Projects all entities that match the given specifications to a new form as specified by the projection expression.
    /// </summary>
    /// <typeparam name="TProjection">The type to project each entity to.</typeparam>
    /// <param name="specifications">A collection of specifications that entities must satisfy.</param>
    /// <param name="projection">An expression that defines the projection from the entity to the new form.</param>
    /// <param name="options">Optional find options to customize query behavior.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a collection of projected
    ///     entities.
    /// </returns>
    Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if an entity with the specified identifier exists in the repository.
    /// </summary>
    /// <param name="id">The identifier of the entity to check for existence.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating whether
    ///     the entity exists.
    /// </returns>
    Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously counts the number of entities that satisfy the provided specifications.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of entities.</returns>
    Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously counts the number of entities that satisfy the specified criteria.
    /// </summary>
    /// <param name="specification">The criteria that entities must satisfy to be counted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the count of entities that satisfy
    ///     the specified criteria.
    /// </returns>
    Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously counts the total number of entities of type TEntity in the repository.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous count operation. The task result contains the total count of entities.</returns>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}