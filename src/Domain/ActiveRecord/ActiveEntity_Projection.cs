// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Projection methods
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Projects all entities to a specified type.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.ProjectAllAsync(c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllAsync<TProjection>(context: null, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities to a specified type using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.ProjectAllAsync(context, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (projection == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TProjection>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.BeforeProjectAllAsync(projection, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TProjection>>.Merge(result,
                    await ctx.Provider.ProjectAllAsync(projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.AfterProjectAllAsync(projection, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given expression to a specified type.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.ProjectAllAsync(c => c.LastName == "Doe", c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, bool>> expression,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllAsync(null, expression, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given expression to a specified type using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.ProjectAllAsync(context, c => c.LastName == "Doe", c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("expression cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TProjection>>.Success([]);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.BeforeProjectAllAsync(projection, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TProjection>>.Merge(result,
                    await ctx.Provider.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.AfterProjectAllAsync(projection, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given specification to a specified type.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.ProjectAllAsync(spec, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllAsync(null, specification, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given specification to a specified type using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.ProjectAllAsync(context, spec, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("specification cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TProjection>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.BeforeProjectAllAsync(projection, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TProjection>>.Merge(result,
                    await ctx.Provider.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.AfterProjectAllAsync(projection, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given specifications to a specified type.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.ProjectAllAsync(specs, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllAsync(null, specifications, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given specifications to a specified type using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.ProjectAllAsync(context, specs, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("specifications cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TProjection>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.BeforeProjectAllAsync(projection, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TProjection>>.Merge(result,
                    await ctx.Provider.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.AfterProjectAllAsync(projection, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given filter model to a specified type.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.ProjectAllAsync(filter, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        FilterModel filter,
        Expression<Func<TEntity, TProjection>> projection,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllAsync(null, filter, projection, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given filter model to a specified type using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the projected entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.ProjectAllAsync(context, filter, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        Expression<Func<TEntity, TProjection>> projection,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("filter cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(Result<IEnumerable<TProjection>>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TProjection>>.Success([]);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.BeforeProjectAllAsync(projection, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TProjection>>.Merge(result,
                    await ctx.Provider.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TProjection>>.Merge(result,
                        await behavior.AfterProjectAllAsync(projection, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities to a specified type with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllPagedAsync(context: null, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities to a specified type with pagination using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(context, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (projection == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, ResultPaged<TProjection>>(
            context,
            async ctx =>
            {
                var result = ResultPaged<TProjection>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.BeforeProjectAllPagedAsync(projection, options, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TProjection>.Merge(result,
                    await ctx.Provider.ProjectAllPagedAsync(projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.AfterProjectAllPagedAsync(projection, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given expression to a specified type with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(c => c.LastName == "Doe", c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        Expression<Func<TEntity, bool>> expression,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllPagedAsync(null, expression, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given expression to a specified type with pagination using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(context, c => c.LastName == "Doe", c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("expression cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, ResultPaged<TProjection>>(
            context,
            async ctx =>
            {
                var result = ResultPaged<TProjection>.Success([], 0, 0, 0);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.BeforeProjectAllPagedAsync(projection, options, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TProjection>.Merge(result,
                    await ctx.Provider.ProjectAllPagedAsync(specification, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.AfterProjectAllPagedAsync(projection, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given specification to a specified type with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(spec, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllPagedAsync(null, specification, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given specification to a specified type with pagination using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(context, spec, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("specification cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, ResultPaged<TProjection>>(
            context,
            async ctx =>
            {
                var result = ResultPaged<TProjection>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.BeforeProjectAllPagedAsync(projection, options, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TProjection>.Merge(result,
                    await ctx.Provider.ProjectAllPagedAsync(specification, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.AfterProjectAllPagedAsync(projection, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given specifications to a specified type with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(specs, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllPagedAsync(null, specifications, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given specifications to a specified type with pagination using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.ProjectAllPagedAsync(context, specs, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("specifications cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, ResultPaged<TProjection>>(
            context,
            async ctx =>
            {
                var result = ResultPaged<TProjection>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.BeforeProjectAllPagedAsync(projection, options, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TProjection>.Merge(result,
                    await ctx.Provider.ProjectAllPagedAsync(specifications, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.AfterProjectAllPagedAsync(projection, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Projects all entities matching the given filter model to a specified type with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .SetPaging(1, 10)
    ///     .Build();
    /// var result = await Customer.ProjectAllPagedAsync(filter, c => c.FirstName);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        FilterModel filter,
        Expression<Func<TEntity, TProjection>> projection,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectAllPagedAsync<TProjection>(null, filter, projection, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities matching the given filter model to a specified type with pagination using the specified context.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged projected entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .SetPaging(1, 10)
    ///     .Build();
    /// var result = await Customer.ProjectAllPagedAsync(context, filter, c => c.FirstName);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        Expression<Func<TEntity, TProjection>> projection,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("filter cannot be null."));
        }

        if (projection == null)
        {
            return Task.FromResult(ResultPaged<TProjection>.Failure("projection cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, ResultPaged<TProjection>>(
            context,
            async ctx =>
            {
                var result = ResultPaged<TProjection>.Success([], 0, 0, 0);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.BeforeProjectAllPagedAsync(projection, options, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TProjection>.Merge(result,
                    await ctx.Provider.ProjectAllPagedAsync(specifications, projection, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TProjection>.Merge(result,
                        (await behavior.AfterProjectAllPagedAsync(projection, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TProjection>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }
}