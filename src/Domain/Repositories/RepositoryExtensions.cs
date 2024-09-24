// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Common;
using Model;
using Specifications;

/// <summary>
///     Provides extension methods for the <see cref="IGenericReadOnlyRepository{TEntity}" /> interface.
/// </summary>
public static class RepositoryExtensions
{
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
        return (await source.ProjectAllAsync(e => e.Id, options, cancellationToken).AnyContext()).Select(i =>
            i.To<TId>());
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
        return (await source.ProjectAllAsync(specification, e => e.Id, options, cancellationToken).AnyContext()).Select(
            i => i.To<TId>());
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