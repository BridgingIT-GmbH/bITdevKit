// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Find methods
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Finds a single entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.FindOneAsync(customerId);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindOneAsync(null, id, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity by its ID using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.FindOneAsync(context, customerId);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static Task<Result<TEntity>> FindOneAsync(
        ActiveEntityContext<TEntity, TId> context,
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == null)
        {
            return Task.FromResult(Result<TEntity>.Failure("id cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(default);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.BeforeFindOneAsync(id, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<TEntity>.Merge(result,
                    await ctx.Provider.FindOneAsync(id, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.AfterFindOneAsync(id, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds a single entity matching the given expression.
    /// </summary>
    /// <param name="expression">The expression to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.FindOneAsync(c => c.Email == "john.doe@example.com");
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> FindOneAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindOneAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity matching the given expression using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.FindOneAsync(context, c => c.Email == "john.doe@example.com");
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static Task<Result<TEntity>> FindOneAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(Result<TEntity>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(default);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.BeforeFindOneAsync(expression, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<TEntity>.Merge(result,
                    await ctx.Provider.FindOneAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.AfterFindOneAsync(expression, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds a single entity matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.Email == "john.doe@example.com");
    /// var result = await Customer.FindOneAsync(spec);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindOneAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity matching the given specification using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.Email == "john.doe@example.com");
    /// var result = await Customer.FindOneAsync(context, spec);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static Task<Result<TEntity>> FindOneAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<TEntity>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(default);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.BeforeFindOneAsync(specification, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<TEntity>.Merge(result,
                    await ctx.Provider.FindOneAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.AfterFindOneAsync(specification, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds a single entity matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.FindOneAsync(specs);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindOneAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity matching the given specifications using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.FindOneAsync(context, specs);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static Task<Result<TEntity>> FindOneAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<TEntity>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(default);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.BeforeFindOneAsync(specifications, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<TEntity>.Merge(result,
                    await ctx.Provider.FindOneAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.AfterFindOneAsync(specifications, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds a single entity matching the given filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.Email, FilterOperator.Equal, "john.doe@example.com")
    ///     .Build();
    /// var result = await Customer.FindOneAsync(filter);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> FindOneAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await FindOneAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity matching the given filter model using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.Email, FilterOperator.Equal, "john.doe@example.com")
    ///     .Build();
    /// var result = await Customer.FindOneAsync(context, filter);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public static Task<Result<TEntity>> FindOneAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(Result<TEntity>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(default);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.BeforeFindOneAsync(filter, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<TEntity>.Merge(result,
                    await ctx.Provider.FindOneAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result,
                        await behavior.AfterFindOneAsync(filter, options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Take = 10 };
    /// var result = await Customer.FindAllAsync(options);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllAsync(context: null, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given options using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var options = new FindOptions<Customer> { Take = 10 };
    /// var result = await Customer.FindAllAsync(context, options);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        ActiveEntityContext<TEntity, TId> context,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TEntity>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.BeforeFindAllAsync(options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TEntity>>.Merge(result,
                    await ctx.Provider.FindAllAsync(options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.AfterFindAllAsync(options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given expression.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.FindAllAsync(c => c.LastName == "Doe");
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given expression using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.FindAllAsync(context, c => c.LastName == "Doe");
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(Result<IEnumerable<TEntity>>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TEntity>>.Success([]);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.BeforeFindAllAsync(options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TEntity>>.Merge(result,
                    await ctx.Provider.FindAllAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.AfterFindAllAsync(options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.FindAllAsync(spec);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given specification using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.FindAllAsync(context, spec);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<IEnumerable<TEntity>>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TEntity>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.BeforeFindAllAsync(options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TEntity>>.Merge(result,
                    await ctx.Provider.FindAllAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.AfterFindAllAsync(options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.FindAllAsync(specs);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given specifications using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.FindAllAsync(context, specs);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<IEnumerable<TEntity>>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TEntity>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.BeforeFindAllAsync(options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TEntity>>.Merge(result,
                    await ctx.Provider.FindAllAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.AfterFindAllAsync(options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.FindAllAsync(filter);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given filter model using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.FindAllAsync(context, filter);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TEntity>>> FindAllAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(Result<IEnumerable<TEntity>>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TEntity>>.Success([]);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.BeforeFindAllAsync(options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TEntity>>.Merge(result,
                    await ctx.Provider.FindAllAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TEntity>>.Merge(result,
                        await behavior.AfterFindAllAsync(options, result.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given expression with pagination.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllPagedAsync(c => c.LastName == "Doe", options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TEntity>> FindAllPagedAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllPagedAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given expression with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllPagedAsync(context, c => c.LastName == "Doe", options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TEntity>> FindAllPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(ResultPaged<TEntity>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TEntity>.Success([], 0, 0, 0);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.BeforeFindAllPagedAsync(options, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TEntity>.Merge(result,
                    await ctx.Provider.FindAllPagedAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.AfterFindAllPagedAsync(options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given specification with pagination.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllPagedAsync(spec, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TEntity>> FindAllPagedAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllPagedAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given specification with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllPagedAsync(context, spec, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TEntity>> FindAllPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(ResultPaged<TEntity>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TEntity>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.BeforeFindAllPagedAsync(options, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TEntity>.Merge(result,
                    await ctx.Provider.FindAllPagedAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.AfterFindAllPagedAsync(options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given specifications with pagination.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllPagedAsync(specs, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TEntity>> FindAllPagedAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllPagedAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given specifications with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllPagedAsync(context, specs, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TEntity>> FindAllPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(ResultPaged<TEntity>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TEntity>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.BeforeFindAllPagedAsync(options, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TEntity>.Merge(result,
                    await ctx.Provider.FindAllPagedAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.AfterFindAllPagedAsync(options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entities matching the given filter model with pagination.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .SetPaging(1, 10)
    ///     .Build();
    /// var result = await Customer.FindAllPagedAsync(filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TEntity>> FindAllPagedAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllPagedAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entities matching the given filter model with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .SetPaging(1, 10)
    ///     .Build();
    /// var result = await Customer.FindAllPagedAsync(context, filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TEntity>> FindAllPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(ResultPaged<TEntity>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TEntity>.Success([], 0, 0, 0);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.BeforeFindAllPagedAsync(options, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TEntity>.Merge(result,
                    await ctx.Provider.FindAllPagedAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TEntity>.Merge(result, (await behavior.AfterFindAllPagedAsync(options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TEntity>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.FindAllIdsAsync();
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsAsync(context: null, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given options using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.FindAllIdsAsync(context);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        ActiveEntityContext<TEntity, TId> context,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TId>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.BeforeFindAllIdsAsync(options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TId>>.Merge(result,
                    await ctx.Provider.FindAllIdsAsync(options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.AfterFindAllIdsAsync(options, result.Value?.Cast<object>(), result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given expression.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.FindAllIdsAsync(c => c.LastName == "Doe");
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given expression using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.FindAllIdsAsync(context, c => c.LastName == "Doe");
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(Result<IEnumerable<TId>>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TId>>.Success([]);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.BeforeFindAllIdsAsync(specification, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TId>>.Merge(result,
                    await ctx.Provider.FindAllIdsAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.AfterFindAllIdsAsync(specification, options, result.Value?.Cast<object>(), result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.FindAllIdsAsync(spec);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given specification using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await Customer.FindAllIdsAsync(context, spec);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<IEnumerable<TId>>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TId>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.BeforeFindAllIdsAsync(specification, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TId>>.Merge(result,
                    await ctx.Provider.FindAllIdsAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.AfterFindAllIdsAsync(specification, options, result.Value?.Cast<object>(), result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.FindAllIdsAsync(specs);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given specifications using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await Customer.FindAllIdsAsync(context, specs);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<IEnumerable<TId>>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TId>>.Success([]);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.BeforeFindAllIdsAsync(specifications, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TId>>.Merge(result,
                    await ctx.Provider.FindAllIdsAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.AfterFindAllIdsAsync(specifications, options, result.Value?.Cast<object>(), result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.FindAllIdsAsync(filter);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given filter model using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .Build();
    /// var result = await Customer.FindAllIdsAsync(context, filter);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public static Task<Result<IEnumerable<TId>>> FindAllIdsAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(Result<IEnumerable<TId>>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = Result<IEnumerable<TId>>.Success([]);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.BeforeFindAllIdsAsync(filter, options, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<IEnumerable<TId>>.Merge(result,
                    await ctx.Provider.FindAllIdsAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<IEnumerable<TId>>.Merge(result,
                        await behavior.AfterFindAllIdsAsync(filter, options, result.Value?.Cast<object>(), result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given options with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsPagedAsync(context: null, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given options with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(context, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TId>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.BeforeFindAllIdsPagedAsync(options, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TId>.Merge(result,
                    await ctx.Provider.FindAllIdsPagedAsync(options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.AfterFindAllIdsPagedAsync(options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given expression with pagination.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(c => c.LastName == "Doe", options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsPagedAsync(null, expression, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given expression with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(context, c => c.LastName == "Doe", options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Task.FromResult(ResultPaged<TId>.Failure("expression cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TId>.Success([], 0, 0, 0);
                var specification = new Specification<TEntity>(expression);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.BeforeFindAllIdsPagedAsync(specification, options, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TId>.Merge(result,
                    await ctx.Provider.FindAllIdsPagedAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.AfterFindAllIdsPagedAsync(specification, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given specification with pagination.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(spec, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsPagedAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given specification with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(context, spec, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(ResultPaged<TId>.Failure("specification cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TId>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.BeforeFindAllIdsPagedAsync(specification, options, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TId>.Merge(result,
                    await ctx.Provider.FindAllIdsPagedAsync(specification, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.AfterFindAllIdsPagedAsync(specification, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given specifications with pagination.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(specs, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsPagedAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given specifications with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var options = new FindOptions<Customer> { Take = 5 };
    /// var result = await Customer.FindAllIdsPagedAsync(context, specs, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(ResultPaged<TId>.Failure("specifications cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TId>.Success([], 0, 0, 0);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.BeforeFindAllIdsPagedAsync(specifications, options, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TId>.Merge(result,
                    await ctx.Provider.FindAllIdsPagedAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.AfterFindAllIdsPagedAsync(specifications, options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Finds all entity IDs matching the given filter model with pagination.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .SetPaging(1, 10)
    ///     .Build();
    /// var result = await Customer.FindAllIdsPagedAsync(filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static async Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        return await FindAllIdsPagedAsync(null, filter, additionalSpecifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all entity IDs matching the given filter model with pagination using the specified context.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For<Customer>()
    ///     .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
    ///     .SetPaging(1, 10)
    ///     .Build();
    /// var result = await Customer.FindAllIdsPagedAsync(context, filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public static Task<ResultPaged<TId>> FindAllIdsPagedAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Task.FromResult(ResultPaged<TId>.Failure("filter cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync(
            context,
            async ctx =>
            {
                var result = ResultPaged<TId>.Success([], 0, 0, 0);

                var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
                var options = FindOptionsBuilder.Build<TEntity>(filter);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.BeforeFindAllIdsPagedAsync(filter, options, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = ResultPaged<TId>.Merge(result,
                    await ctx.Provider.FindAllIdsPagedAsync(specifications, options, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = ResultPaged<TId>.Merge(result, (await behavior.AfterFindAllIdsPagedAsync(options, result, result.IsSuccess, cancellationToken).AnyContext()).ToResultPaged<TId>());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }
}