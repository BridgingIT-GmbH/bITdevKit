// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Count methods
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Counts entities matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.CountAsync();
    /// if (result.IsSuccess) { Console.WriteLine($"Total entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> CountAsync(CancellationToken cancellationToken = default)
    {
        return await CountAsync(null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Counts entities matching the given options using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.CountAsync(context);
    /// if (result.IsSuccess) { Console.WriteLine($"Total entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> CountAsync(
        ActiveEntityContext<TEntity, TId> context,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success(0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.BeforeCountAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<long>.Merge(result,
                    await ctx.Provider.CountAsync(cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.AfterCountAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Counts entities matching the given expression.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.CountAsync(c => c.LastName == "Doe");
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> CountAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await CountAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Counts entities matching the given expression using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.CountAsync(context, c => c.LastName == "Doe");
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> CountAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(Result<long>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success(0);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.BeforeCountAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<long>.Merge(result,
                    await ctx.Provider.CountAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.AfterCountAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Counts entities matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.CountAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> CountAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await CountAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Counts entities matching the given specification using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.CountAsync(context, spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> CountAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<long>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success(0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.BeforeCountAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<long>.Merge(result,
                    await ctx.Provider.CountAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.AfterCountAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Counts entities matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.CountAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await CountAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Counts entities matching the given specifications using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.CountAsync(context, specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> CountAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<long>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success(0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.BeforeCountAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<long>.Merge(result,
                    await ctx.Provider.CountAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.AfterCountAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Counts entities matching the given filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.CountAsync(filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> CountAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await CountAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Counts entities matching the given filter model using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.CountAsync(context, filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> CountAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(Result<long>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success(0);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.BeforeCountAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<long>.Merge(result,
                    await ctx.Provider.CountAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result,
                        await behavior.AfterCountAsync(result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }
}