// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Specifications;

/// <summary>
/// Provides validation extension methods for Result objects that work with repositories.
/// </summary>
public static class ResultValidationExtensions
{
    /// <summary>
    /// Ensures that a property value is unique in the repository.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="result">The result to validate</param>
    /// <param name="repository">The repository to check</param>
    /// <param name="propertyExpression">Expression for the property to check (e.g., x => x.Name)</param>
    /// <param name="value">The value to check for uniqueness</param>
    /// <param name="errorMessage">The error message if validation fails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result</returns>
    public static async Task<Result<TEntity>> EnsureUniqueAsync<TEntity>(
        this Result<TEntity> result,
        IGenericRepository<TEntity> repository,
        Expression<Func<TEntity, object>> propertyExpression,
        object value,
        string errorMessage,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        return await result.EnsureAsync(
            async (entity, ct) =>
            {
                var count = await repository.CountAsync(
                    new UniqueSpecification<TEntity>(propertyExpression, value),
                    cancellationToken: ct);
                return count == 0;
            },
            new Error(errorMessage),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Ensures that a property value is unique in the repository, excluding a specific entity (for updates).
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TId">The entity ID type</typeparam>
    /// <param name="result">The result to validate</param>
    /// <param name="repository">The repository to check</param>
    /// <param name="propertyExpression">Expression for the property to check (e.g., x => x.Name)</param>
    /// <param name="value">The value to check for uniqueness</param>
    /// <param name="excludeId">The ID of the entity to exclude from the check</param>
    /// <param name="errorMessage">The error message if validation fails</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result</returns>
    public static async Task<Result<TEntity>> EnsureUniqueExceptAsync<TEntity, TId>(
        this Result<TEntity> result,
        IGenericRepository<TEntity> repository,
        Expression<Func<TEntity, object>> propertyExpression,
        object value,
        TId excludeId,
        string errorMessage,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TId>
    {
        return await result.EnsureAsync(
            async (entity, ct) =>
            {
                var count = await repository.CountAsync(
                    new UniqueExceptSpecification<TEntity, TId>(propertyExpression, value, excludeId),
                    cancellationToken: ct);
                return count == 0;
            },
            new Error(errorMessage),
            cancellationToken: cancellationToken);
    }
}
