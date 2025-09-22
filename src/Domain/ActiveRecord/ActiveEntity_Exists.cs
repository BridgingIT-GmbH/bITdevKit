// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Exists methods
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Checks if any entities exist matching the given id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.ExistsAsync(1);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    public static async Task<Result<bool>> ExistsAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        return await ExistsAsync(null, id, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given id using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.ExistsAsync(context, 1);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    public static Task<Result<bool>> ExistsAsync(
        ActiveEntityContext<TEntity, TId> context,
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == null)
        {
            return Task.FromResult(Result<bool>.Failure("id cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<bool>>(
            context,
            async ctx =>
            {
                var result = Result<bool>.Success(false);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.BeforeExistsAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action (FindOneAsync to check existence)
                var entityResult = await ctx.Provider.FindOneAsync(id, null, cancellationToken).AnyContext();
                result = Result<bool>.Merge(result, entityResult.IsSuccess && entityResult.Value != null);

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.AfterExistsAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Checks if any entities exist matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.ExistsAsync();
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    public static async Task<Result<bool>> ExistsAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ExistsAsync(context: null, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given options using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static Task<Result<bool>> ExistsAsync(
        ActiveEntityContext<TEntity, TId> context,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<bool>>(
            context,
            async ctx =>
            {
                var result = Result<bool>.Success(false);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.BeforeExistsAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<bool>.Merge(result,
                    await ctx.Provider.ExistsAsync(options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.AfterExistsAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Checks if any entities exist matching the given expression.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.ExistsAsync(c => c.LastName == "Doe");
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    public static async Task<Result<bool>> ExistsAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ExistsAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given expression using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static Task<Result<bool>> ExistsAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(Result<bool>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<bool>>(
            context,
            async ctx =>
            {
                var result = Result<bool>.Success(false);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.BeforeExistsAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<bool>.Merge(result,
                    await ctx.Provider.ExistsAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.AfterExistsAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Checks if any entities exist matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static async Task<Result<bool>> ExistsAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ExistsAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given specification using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static Task<Result<bool>> ExistsAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<bool>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<bool>>(
            context,
            async ctx =>
            {
                var result = Result<bool>.Success(false);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.BeforeExistsAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<bool>.Merge(result,
                    await ctx.Provider.ExistsAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.AfterExistsAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Checks if any entities exist matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static async Task<Result<bool>> ExistsAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ExistsAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given specifications using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static Task<Result<bool>> ExistsAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<bool>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<bool>>(
            context,
            async ctx =>
            {
                var result = Result<bool>.Success(false);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.BeforeExistsAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<bool>.Merge(result,
                    await ctx.Provider.ExistsAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.AfterExistsAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Checks if any entities exist matching the given filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static async Task<Result<bool>> ExistsAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await ExistsAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given filter model using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    public static Task<Result<bool>> ExistsAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(Result<bool>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<bool>>(
            context,
            async ctx =>
            {
                var result = Result<bool>.Success(false);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.BeforeExistsAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<bool>.Merge(result,
                    await ctx.Provider.ExistsAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<bool>.Merge(result,
                        await behavior.AfterExistsAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }
}