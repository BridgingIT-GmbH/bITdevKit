// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;

public abstract partial class ActiveEntity<TEntity, TId> : Entity<TId> // Write support
    where TEntity : ActiveEntity<TEntity, TId>
{
    /// <summary>
    /// Inserts the current entity into the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the inserted entity.</returns>
    /// <example>
    /// <code>
    /// var customer = new Customer { FirstName = "John", LastName = "Doe" };
    /// var result = await customer.InsertAsync();
    /// if (result.IsSuccess) { Console.WriteLine($"Inserted customer with ID: {result.Value.Id}"); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> InsertAsync(CancellationToken cancellationToken = default)
    {
        return await this.InsertAsync(null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Inserts the current entity into the underlying storage.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the inserted entity.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customer = new Customer { FirstName = "John", LastName = "Doe" };
    /// var result = await customer.InsertAsync(context);
    /// if (result.IsSuccess) { Console.WriteLine($"Inserted customer with ID: {result.Value.Id}"); }
    /// </code>
    /// </example>
    public Task<Result<TEntity>> InsertAsync(ActiveEntityContext<TEntity, TId> context, CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<TEntity>>(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(this.Self);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result, await behavior.BeforeInsertAsync(this.Self, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // create callbacks
                var callbacks = new ActiveEntityCallbackOptions<TEntity, TId>
                {
                    BeforeInsertAsync = this.OnBeforeInsertAsync,
                    AfterInsertAsync = this.OnAfterInsertAsync,
                    BeforeUpsertAsync = this.OnBeforeUpsertAsync,
                    AfterUpsertAsync = this.OnAfterUpsertAsync
                };

                // provider action
                result = Result<TEntity>.Merge(result, await ctx.Provider.InsertAsync(this.Self, callbacks, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result, await behavior.AfterInsertAsync(this.Self, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Inserts multiple entities into the underlying storage.
    /// This is a convenience method that sequentially calls InsertAsync on each entity.
    /// </summary>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results, each containing the inserted entity or errors.</returns>
    /// <example>
    /// <code>
    /// var customers = new[] { new Customer { FirstName = "John" }, new Customer { FirstName = "Jane" } };
    /// var results = await Customer.InsertAsync(customers);
    /// foreach (var result in results) {
    ///     if (result.IsSuccess) { Console.WriteLine($"Inserted: {result.Value.FirstName}"); }
    /// }
    /// </code>
    /// </example>
    public static async Task<IEnumerable<Result<TEntity>>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return await InsertAsync(null, entities, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Inserts multiple entities into the underlying storage using the specified provider.
    /// This is a convenience method that sequentially calls InsertAsync on each entity.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results, each containing the inserted entity or errors.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customers = new[] { new Customer { FirstName = "John" }, new Customer { FirstName = "Jane" } };
    /// var results = await Customer.InsertAsync(context, customers);
    /// foreach (var result in results) {
    ///     if (result.IsSuccess) { Console.WriteLine($"Inserted: {result.Value.FirstName}"); }
    /// }
    /// </code>
    /// </example>
    public static Task<IEnumerable<Result<TEntity>>> InsertAsync(
         ActiveEntityContext<TEntity, TId> context,
         IEnumerable<TEntity> entities,
         CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, IEnumerable<Result<TEntity>>>(
            context,
            async ctx =>
            {
                var results = new List<Result<TEntity>>();

                foreach (var entity in entities.SafeNull())
                {
                    results.Add(await entity.InsertAsync(ctx, cancellationToken).AnyContext());
                }

                return results;
            });
    }

    /// <summary>
    /// Static helper to insert a single entity (context resolved via DI).
    /// </summary>
    /// <param name="entity">Entity instance to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing inserted entity.</returns>
    /// <example>
    /// <code>
    /// var customer = new Customer { FirstName = "John" };
    /// var result = await Customer.InsertAsync(customer);
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return Result<TEntity>.Failure("entity cannot be null.");
        }

        return await entity.InsertAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts the entity into the underlying storage.
    /// </summary>
    /// <param name="context">Active Entity context (uses DI if null).</param>
    /// <param name="entity">Entity instance to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing inserted entity.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(provider, behaviors);
    /// var customer = new Customer { FirstName = "John" };
    /// var result = await Customer.InsertAsync(context, customer);
    /// </code>
    /// </example>
    public static async Task<Result<TEntity>> InsertAsync(ActiveEntityContext<TEntity, TId> context, TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return Result<TEntity>.Failure("entity cannot be null.");
        }

        return await entity.InsertAsync(context, cancellationToken);
    }

    /// <summary>
    /// Updates the current entity in the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the updated entity.</returns>
    /// <example>
    /// <code>
    /// var customer = await Customer.FindOneAsync(customerId);
    /// customer.Value.FirstName = "Jane";
    /// var result = await customer.Value.UpdateAsync();
    /// if (result.IsSuccess) { Console.WriteLine("Customer updated"); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> UpdateAsync(CancellationToken cancellationToken = default)
    {
        return await this.UpdateAsync(null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates the current entity in the underlying storage.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the updated entity.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customer = await Customer.FindOneAsync(customerId);
    /// customer.Value.FirstName = "Jane";
    /// var result = await customer.Value.UpdateAsync(context);
    /// if (result.IsSuccess) { Console.WriteLine("Customer updated"); }
    /// </code>
    /// </example>
    public Task<Result<TEntity>> UpdateAsync(ActiveEntityContext<TEntity, TId> context, CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<TEntity>>(
            context,
            async ctx =>
            {
                var result = Result<TEntity>.Success(this.Self);

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result, await behavior.BeforeUpdateAsync(this.Self, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // create callbacks
                var callbacks = new ActiveEntityCallbackOptions<TEntity, TId>
                {
                    BeforeUpdateAsync = this.OnBeforeUpdateAsync,
                    AfterUpdateAsync = this.OnAfterUpdateAsync,
                    BeforeUpsertAsync = this.OnBeforeUpsertAsync,
                    AfterUpsertAsync = this.OnAfterUpsertAsync
                };

                // provider action
                result = Result<TEntity>.Merge(result, await ctx.Provider.UpdateAsync(this.Self, callbacks, cancellationToken).AnyContext());
                if (result.IsFailure) return result;

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<TEntity>.Merge(result, await behavior.AfterUpdateAsync(this.Self, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Updates entities matching the given expression by setting the specified properties.
    /// </summary>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// var result = await User.UpdateSetAsync(
    ///     u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-1),
    ///     set => set.Set(u => u.IsActive, false));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> UpdateAsync(
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (set == null)
        {
            return Result.Failure("update set cannot be null.");
        }

        // no callbacks here as this is a direct set update without loading the entity first

        var result = await UpdateSetAsync(null, e => e.Id.Equals(this.Self.Id), set, options, cancellationToken).AnyContext(); // use the set update to directly update the entity in the provider (db)

        //if (result.Value == 0)
        //{
        //    //return Result<TEntity>.Failure(this.Self, "no entity updated");
        //    return result.Map(v => this.Self).WithError("no entity updated");
        //}

        if (result.IsSuccess) // apply the changes to the current entity instance as the changes where applied with the provider (db)
        {
            var updateBuilder = new EntityUpdateSet<TEntity>();
            set(updateBuilder);
            updateBuilder.Apply(this.Self);
        }

        return result.Map(v => this.Self);
    }

    /// <summary>
    /// Updates multiple entities in the underlying storage.
    /// This is a convenience method that sequentially calls UpdateAsync on each entity.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results, each containing the updated entity or errors.</returns>
    /// <example>
    /// <code>
    /// var customers = await Customer.FindAllAsync();
    /// foreach (var customer in customers.Value) { customer.FirstName += " Updated"; }
    /// var results = await Customer.UpdateAsync(customers.Value);
    /// foreach (var result in results) {
    ///     if (result.IsSuccess) { Console.WriteLine($"Updated: {result.Value.FirstName}"); }
    /// }
    /// </code>
    /// </example>
    public static async Task<IEnumerable<Result<TEntity>>> UpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(null, entities, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates multiple entities in the underlying storage using the specified provider.
    /// This is a convenience method that sequentially calls UpdateAsync on each entity.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="entities">The entities to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results, each containing the updated entity or errors.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customers = await Customer.FindAllAsync();
    /// foreach (var customer in customers.Value) { customer.FirstName += " Updated"; }
    /// var results = await Customer.UpdateAsync(context, customers.Value);
    /// foreach (var result in results) {
    ///     if (result.IsSuccess) { Console.WriteLine($"Updated: {result.Value.FirstName}"); }
    /// }
    /// </code>
    /// </example>
    public static Task<IEnumerable<Result<TEntity>>> UpdateAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, IEnumerable<Result<TEntity>>>(
            context,
            async ctx =>
            {
                var results = new List<Result<TEntity>>();

                foreach (var entity in entities.SafeNull())
                {
                    results.Add(await entity.UpdateAsync(ctx, cancellationToken).AnyContext());
                }

                return results;
            });
    }

    /// <summary>
    /// Updates entities matching the given specification by setting the specified properties.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification&lt;User&gt;(u => u.IsDeleted);
    /// var result = await User.UpdateSetAsync(spec, set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Result.Failure("specification cannot be null.");
        }

        return await UpdateSetAsync(null, specification, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates entities matching the given specifications by setting the specified properties.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] {
    ///     new Specification&lt;User&gt;(u => u.IsDeleted),
    ///     new Specification&lt;User&gt;(u => !u.IsActive)
    /// };
    /// var result = await User.UpdateSetAsync(specs, set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Archived {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Result.Failure("specifications cannot be null.");
        }

        return await UpdateSetAsync(null, specifications, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates entities matching the given expression by setting the specified properties.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// var result = await User.UpdateSetAsync(
    ///     u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-1),
    ///     set => set.Set(u => u.IsActive, false));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> UpdateSetAsync(
        Expression<Func<TEntity, bool>> expression,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Result.Failure("expression cannot be null.");
        }

        var specification = new Specification<TEntity>(expression);

        return await UpdateSetAsync(null, specification, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates entities matching the given filter model by setting the specified properties.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For&lt;User&gt;()
    ///     .AddFilter(u => u.IsDeleted, FilterOperator.Equal, true)
    ///     .Build();
    /// var result = await User.UpdateSetAsync(filter, set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> UpdateSetAsync(
        FilterModel filter,
        Action<IEntityUpdateSet<TEntity>> set,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Result.Failure("filter cannot be null.");
        }

        if (set == null)
        {
            return Result.Failure("update set cannot be null.");
        }

        var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
        var options = FindOptionsBuilder.Build<TEntity>(filter);

        return await UpdateSetAsync(null, specifications, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates entities matching the given specification by setting the specified properties,
    /// using the provided Active Entity context (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity context to use (or null to resolve from DI).</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<User, Guid>(...);
    /// var result = await User.UpdateSetAsync(context,
    ///     new Specification&lt;User&gt;(u => u.IsDeleted),
    ///     set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> UpdateSetAsync(
        ActiveEntityContext<TEntity, TId> context,
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Task.FromResult(Result<long>.Failure("specification cannot be null."));
        }

        if (set == null)
        {
            return Task.FromResult(Result<long>.Failure("update set cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success();

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result, await behavior.BeforeUpdateSetAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                // provider action
                result = Result<long>.Merge(result, await ctx.Provider.UpdateSetAsync(specification, set, options, cancellationToken).AnyContext());

                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result, await behavior.AfterUpdateSetAsync(result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Updates entities matching the given specifications by setting the specified properties,
    /// using the provided Active Entity context (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity context to use (or null to resolve from DI).</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<User, Guid>(...);
    /// var specs = new[] {
    ///     new Specification&lt;User&gt;(u => u.IsDeleted),
    ///     new Specification&lt;User&gt;(u => !u.IsActive)
    /// };
    /// var result = await User.UpdateSetAsync(context, specs, set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Archived {result.Value} users"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> UpdateSetAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Task.FromResult(Result<long>.Failure("specifications cannot be null."));
        }

        if (set == null)
        {
            return Task.FromResult(Result<long>.Failure("update set cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<long>>(
            context,
            async ctx =>
            {
                var result = Result<long>.Success();

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result, await behavior.BeforeUpdateSetAsync(cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                result = Result<long>.Merge(result, await ctx.Provider.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext());

                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<long>.Merge(result, await behavior.AfterUpdateSetAsync(result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Updates entities matching the given expression by setting the specified properties,
    /// using the provided Active Entity provider (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity provider to use (or null to resolve from DI).</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var provider = scope.ServiceProvider.GetRequiredService&lt;IActiveEntityEntityProvider&lt;User, Guid&gt;&gt;();
    /// var result = await User.UpdateSetAsync(provider,
    ///     u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-1),
    ///     set => set.Set(u => u.IsActive, false));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> UpdateSetAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Result.Failure("expression cannot be null.");
        }

        if (set == null)
        {
            return Result.Failure("update set cannot be null.");
        }

        var specification = new Specification<TEntity>(expression);

        // no callbacks here as this is a direct set update without loading the entity first

        return await UpdateSetAsync(context, specification, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Updates entities matching the given filter model by setting the specified properties,
    /// using the provided Active Entity provider (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity provider to use (or null to resolve from DI).</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var provider = scope.ServiceProvider.GetRequiredService&lt;IActiveEntityEntityProvider&lt;User, Guid&gt;&gt;();
    /// var filter = FilterModelBuilder.For&lt;User&gt;()
    ///     .AddFilter(u => u.IsDeleted, FilterOperator.Equal, true)
    ///     .Build();
    /// var result = await User.UpdateSetAsync(provider, filter, set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> UpdateSetAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        Action<IEntityUpdateSet<TEntity>> set,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Result.Failure("filter cannot be null.");
        }

        if (set == null)
        {
            return Result.Failure("update set cannot be null.");
        }

        var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
        var options = FindOptionsBuilder.Build<TEntity>(filter);

        // no callbacks here as this is a direct set update without loading the entity first

        return await UpdateSetAsync(context, specifications, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Upserts the current entity (inserts if new, updates if exists) in the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the entity and the action performed (Inserted/Updated).</returns>
    /// <example>
    /// <code>
    /// var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
    /// var result = await customer.UpsertAsync();
    /// Console.WriteLine($"Action: {result.Value.action}"); // Inserted or Updated
    /// </code>
    /// </example>
    public async Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertAsync(CancellationToken cancellationToken = default)
    {
        return await this.UpsertAsync(null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Upserts the current entity (inserts if new, updates if exists) in the underlying storage.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the entity and the action performed (Inserted/Updated).</returns>
    /// <example>
    /// <code>
    /// var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
    /// var result = await customer.UpsertAsync();
    /// Console.WriteLine($"Action: {result.Value.action}"); // Inserted or Updated
    /// </code>
    /// </example>
    public Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertAsync(
        ActiveEntityContext<TEntity, TId> context,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result<(TEntity entity, RepositoryActionResult action)>>(
            context,
            async ctx =>
            {
                var result = Result<(TEntity, RepositoryActionResult)>.Success((this.Self, RepositoryActionResult.None));

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<(TEntity, RepositoryActionResult)>.Merge(result, await behavior.BeforeUpsertAsync(this.Self, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                var callbacks = new ActiveEntityCallbackOptions<TEntity, TId>
                {
                    BeforeInsertAsync = this.OnBeforeInsertAsync,
                    AfterInsertAsync = this.OnAfterInsertAsync,
                    BeforeUpdateAsync = this.OnBeforeUpdateAsync,
                    AfterUpdateAsync = this.OnAfterUpdateAsync,
                    BeforeUpsertAsync = this.OnBeforeUpsertAsync,
                    AfterUpsertAsync = this.OnAfterUpsertAsync
                };

                // provider action
                result = Result<(TEntity, RepositoryActionResult)>.Merge(result, await ctx.Provider.UpsertAsync(this.Self, callbacks, cancellationToken).AnyContext());

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result<(TEntity, RepositoryActionResult)>.Merge(result, await behavior.AfterUpsertAsync(this.Self, result.IsSuccess ? result.Value.Item2 : RepositoryActionResult.None, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Upserts multiple entities (inserts if new, updates if exists) in the underlying storage.
    /// This is a convenience method that sequentially calls UpsertAsync on each entity.
    /// </summary>
    /// <param name="entities">The entities to upsert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results, each containing the entity and the action performed (Inserted/Updated) or errors.</returns>
    /// <example>
    /// <code>
    /// var customers = new[] { new Customer { Id = existingId, FirstName = "John" }, new Customer { FirstName = "Jane" } };
    /// var results = await Customer.UpsertAsync(customers);
    /// foreach (var result in results) {
    ///     if (result.IsSuccess) { Console.WriteLine($"Action: {result.Value.action} for {result.Value.entity.FirstName}"); }
    /// }
    /// </code>
    /// </example>
    public static async Task<IEnumerable<Result<(TEntity entity, RepositoryActionResult action)>>> UpsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return await UpsertAsync(null, entities, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Upserts multiple entities (inserts if new, updates if exists) in the underlying storage using the specified provider.
    /// This is a convenience method that sequentially calls UpsertAsync on each entity.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="entities">The entities to upsert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results, each containing the entity and the action performed (Inserted/Updated) or errors.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customers = new[] { new Customer { Id = existingId, FirstName = "John" }, new Customer { FirstName = "Jane" } };
    /// var results = await Customer.UpsertAsync(context, customers);
    /// foreach (var result in results) {
    ///     if (result.IsSuccess) { Console.WriteLine($"Action: {result.Value.action} for {result.Value.entity.FirstName}"); }
    /// }
    /// </code>
    /// </example>
    public static Task<IEnumerable<Result<(TEntity entity, RepositoryActionResult action)>>> UpsertAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, IEnumerable<Result<(TEntity entity, RepositoryActionResult action)>>>(
            context,
            async ctx =>
            {
                var results = new List<Result<(TEntity entity, RepositoryActionResult action)>>();

                foreach (var entity in entities.SafeNull())
                {
                    results.Add(await entity.UpsertAsync(ctx, cancellationToken).AnyContext());
                }

                return results;
            });
    }

    /// <summary>
    /// Deletes the current entity from the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var customer = await Customer.FindOneAsync(customerId);
    /// var result = await customer.Value.DeleteAsync();
    /// if (result.IsSuccess) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public async Task<Result> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return await this.DeleteAsync(null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes the current entity from the underlying storage.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customer = await Customer.FindOneAsync(customerId);
    /// var result = await customer.Value.DeleteAsync(context);
    /// if (result.IsSuccess) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public Task<Result> DeleteAsync(ActiveEntityContext<TEntity, TId> context, CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result>(
            context,
            async ctx =>
            {
                var result = Result.Success();

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result.Merge(result, await behavior.BeforeDeleteAsync(this.Self, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                var callbacks = new ActiveEntityCallbackOptions<TEntity, TId>
                {
                    BeforeDeleteAsync = this.OnBeforeDeleteAsync,
                    AfterDeleteAsync = this.OnAfterDeleteAsync,
                };

                // provider action
                result = Result.Merge(result, await ctx.Provider.DeleteAsync(this.Self, callbacks, cancellationToken).AnyContext());

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result.Merge(result, await behavior.AfterDeleteAsync(this.Self, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Deletes an entity by its ID from the underlying storage.
    /// </summary>
    /// <param name="id">The ID of the entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the action performed (Deleted/None/NotFound).</returns>
    /// <example>
    /// <code>
    /// var result = await Customer.DeleteAsync(customerId);
    /// if (result.IsSuccess && result.Value == RepositoryActionResult.Deleted) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public static async Task<Result> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return await DeleteAsync(null, id, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes an entity by its ID from the underlying storage.
    /// </summary>
    /// <param name="context">Context to use for the operation; uses DI if null.</param>
    /// <param name="id">The ID of the entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the action performed (Deleted/None/NotFound).</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await Customer.DeleteAsync(context, customerId);
    /// if (result.IsSuccess && result.Value == RepositoryActionResult.Deleted) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public static Task<Result> DeleteAsync(
        ActiveEntityContext<TEntity, TId> context,
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == null)
        {
            return Task.FromResult(Result.Failure("Id cannot be null."));
        }

        return ActiveEntityContextScope.UseAsync<TEntity, TId, Result>(
            context,
            async ctx =>
            {
                // find entity
                var entityResult = await ctx.Provider.FindOneAsync(id, null, cancellationToken).AnyContext();
                if (entityResult.IsFailure || entityResult.Value == null)
                {
                    return entityResult;
                }

                var result = Result.Success();

                // before behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result.Merge(result, await behavior.BeforeDeleteAsync(entityResult.Value, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                var callbacks = new ActiveEntityCallbackOptions<TEntity, TId>
                {
                    BeforeDeleteAsync = entityResult.Value.OnBeforeDeleteAsync,
                    AfterDeleteAsync = entityResult.Value.OnAfterDeleteAsync,
                };

                // provider action
                result = Result.Merge(result, await ctx.Provider.DeleteAsync(entityResult.Value, callbacks, cancellationToken).AnyContext());

                // after behaviors
                foreach (var behavior in ctx.Behaviors)
                {
                    result = Result.Merge(result, await behavior.AfterDeleteAsync(entityResult.Value, result.IsSuccess, cancellationToken).AnyContext());
                    if (result.IsFailure) return result;
                }

                return result;
            });
    }

    /// <summary>
    /// Deletes multiple entities from the underlying storage.
    /// This is a convenience method that sequentially calls DeleteAsync on each entity.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results indicating success or failure for each deletion.</returns>
    /// <example>
    /// <code>
    /// var customers = await Customer.FindAllAsync(c => c.IsDeleted);
    /// var results = await Customer.DeleteAsync(customers.Value);
    /// var deletedCount = results.Count(r => r.IsSuccess);
    /// Console.WriteLine($"Deleted {deletedCount} customers");
    /// </code>
    /// </example>
    public static async Task<IEnumerable<Result>> DeleteAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        return await DeleteAsync(null, entities, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes multiple entities from the underlying storage using the specified context.
    /// This is a convenience method that sequentially calls DeleteAsync on each entity.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results indicating success or failure for each deletion.</returns>
    /// <example>
    /// <code>
    /// using var scope = serviceProvider.CreateScope();
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var customers = await Customer.FindAllAsync(c => c.IsDeleted);
    /// var results = await Customer.DeleteAsync(context, customers.Value);
    /// var deletedCount = results.Count(r => r.IsSuccess);
    /// Console.WriteLine($"Deleted {deletedCount} customers");
    /// </code>
    /// </example>
    public static Task<IEnumerable<Result>> DeleteAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, IEnumerable<Result>>(
            context,
            async ctx =>
            {
                var results = new List<Result>();

                foreach (var entity in entities.SafeNull())
                {
                    results.Add(await entity.DeleteAsync(ctx, cancellationToken).AnyContext());
                }

                return results;
            });
    }

    /// <summary>
    /// Deletes multiple entities by their IDs from the underlying storage.
    /// This is a convenience method that sequentially calls DeleteAsync on each ID.
    /// </summary>
    /// <param name="ids">The IDs of the entities to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results containing the action performed (Deleted/None/NotFound) for each ID.</returns>
    /// <example>
    /// <code>
    /// var ids = new[] { customerId1, customerId2 };
    /// var results = await Customer.DeleteAsync(ids);
    /// var deletedCount = results.Count(r => r.IsSuccess && r.Value == RepositoryActionResult.Deleted);
    /// Console.WriteLine($"Deleted {deletedCount} customers");
    /// </code>
    /// </example>
    public async static Task<IEnumerable<Result>> DeleteAsync(
        IEnumerable<object> ids,
        CancellationToken cancellationToken = default)
    {
        return await DeleteAsync(null, ids, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes multiple entities by their IDs from the underlying storage.
    /// This is a convenience method that sequentially calls DeleteAsync on each ID.
    /// </summary>
    /// <param name="context">Optional context to use for the operation; uses DI if null.</param>
    /// <param name="ids">The IDs of the entities to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a collection of Results containing the action performed (Deleted/None/NotFound) for each ID.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var ids = new[] { customerId1, customerId2 };
    /// var results = await Customer.DeleteAsync(context, ids);
    /// var deletedCount = results.Count(r => r.IsSuccess && r.Value == RepositoryActionResult.Deleted);
    /// Console.WriteLine($"Deleted {deletedCount} customers");
    /// </code>
    /// </example>
    public static Task<IEnumerable<Result>> DeleteAsync(
        ActiveEntityContext<TEntity, TId> context,
        IEnumerable<object> ids,
        CancellationToken cancellationToken = default)
    {
        return ActiveEntityContextScope.UseAsync<TEntity, TId, IEnumerable<Result>>(
           context,
           async ctx =>
           {
               var results = new List<Result>();

               foreach (var id in ids.SafeNull())
               {
                   results.Add(await DeleteAsync(ctx, id, cancellationToken).AnyContext());
               }

               return results;
           });
    }

    /// <summary>
    /// Deletes entities matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification&lt;User&gt;(u => u.IsDeleted);
    /// var result = await User.DeleteSetAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
        {
            return Result.Failure("specification cannot be null.");
        }

        return await DeleteSetAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes entities matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] {
    ///     new Specification&lt;User&gt;(u => u.IsDeleted),
    ///     new Specification&lt;User&gt;(u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-5))
    /// };
    /// var result = await User.DeleteSetAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (specifications == null)
        {
            return Result.Failure("specifications cannot be null.");
        }

        return await DeleteSetAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes entities matching the given expression.
    /// </summary>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var result = await User.DeleteSetAsync(u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-5));
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> DeleteSetAsync(
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Result.Failure("expression cannot be null.");
        }

        var specification = new Specification<TEntity>(expression);

        return await DeleteSetAsync(null, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes entities matching the given filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var filter = FilterModelBuilder.For&lt;User&gt;()
    ///     .AddFilter(u => u.IsDeleted, FilterOperator.Equal, true)
    ///     .Build();
    /// var result = await User.DeleteSetAsync(filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> DeleteSetAsync(
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Result.Failure("filter cannot be null.");
        }

        var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
        var options = FindOptionsBuilder.Build<TEntity>(filter);

        return await DeleteSetAsync(null, specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes entities matching the given specification,
    /// using the provided Active Entity context (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity context to use (or null to resolve from DI).</param>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<User, Guid>(...);
    /// var result = await User.DeleteSetAsync(context, new Specification&lt;User&gt;(u => u.IsDeleted));
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> DeleteSetAsync(
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
                foreach (var behavior in ctx.Behaviors)
                {
                    var behaviorResult = await behavior.BeforeDeleteSetAsync(cancellationToken).AnyContext();
                    if (behaviorResult.IsFailure)
                    {
                        return behaviorResult;
                    }
                }

                var result = await ctx.Provider.DeleteSetAsync(specification, options, cancellationToken).AnyContext();

                foreach (var behavior in ctx.Behaviors)
                {
                    var behaviorResult = await behavior.AfterDeleteSetAsync(result.IsSuccess, cancellationToken).AnyContext();
                    if (behaviorResult.IsFailure)
                    {
                        return behaviorResult;
                    }
                }

                return result;
            });
    }

    /// <summary>
    /// Deletes entities matching the given specifications,
    /// using the provided Active Entity context (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity context to use (or null to resolve from DI).</param>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<User, Guid>(...);
    /// var specs = new[] {
    ///     new Specification&lt;User&gt;(u => u.IsDeleted),
    ///     new Specification&lt;User&gt;(u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-5))
    /// };
    /// var result = await User.DeleteSetAsync(context, specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static Task<Result<long>> DeleteSetAsync(
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
                foreach (var behavior in ctx.Behaviors)
                {
                    var behaviorResult = await behavior.BeforeDeleteSetAsync(cancellationToken).AnyContext();
                    if (behaviorResult.IsFailure)
                    {
                        return behaviorResult;
                    }
                }

                var result = await ctx.Provider.DeleteSetAsync(specifications, options, cancellationToken).AnyContext();

                foreach (var behavior in ctx.Behaviors)
                {
                    var behaviorResult = await behavior.AfterDeleteSetAsync(result.IsSuccess, cancellationToken).AnyContext();
                    if (behaviorResult.IsFailure)
                    {
                        return behaviorResult;
                    }
                }

                return result;
            });
    }

    /// <summary>
    /// Deletes entities matching the given expression,
    /// using the provided Active Entity provider (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity provider to use (or null to resolve from DI).</param>
    /// <param name="expression">The expression to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var result = await User.DeleteSetAsync(context, u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-5));
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> DeleteSetAsync(
        ActiveEntityContext<TEntity, TId> context,
        Expression<Func<TEntity, bool>> expression,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (expression == null)
        {
            return Result.Failure("expression cannot be null.");
        }

        var specification = new Specification<TEntity>(expression);

        return await DeleteSetAsync(context, specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes entities matching the given filter model,
    /// using the provided Active Entity provider (or resolving one if null).
    /// </summary>
    /// <param name="context">The Active Entity provider to use (or null to resolve from DI).</param>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="additionalSpecifications">Optional additional specifications to combine with the filter.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of deleted rows.</returns>
    /// <example>
    /// <code>
    /// var context = new ActiveEntityContext<Customer, Guid>(...);
    /// var filter = FilterModelBuilder.For&lt;User&gt;()
    ///     .AddFilter(u => u.IsDeleted, FilterOperator.Equal, true)
    ///     .Build();
    /// var result = await User.DeleteSetAsync(context, filter);
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public static async Task<Result<long>> DeleteSetAsync(
        ActiveEntityContext<TEntity, TId> context,
        FilterModel filter,
        IEnumerable<ISpecification<TEntity>> additionalSpecifications = null,
        CancellationToken cancellationToken = default)
    {
        if (filter == null)
        {
            return Result.Failure("filter cannot be null.");
        }

        var specifications = SpecificationBuilder.Build(filter, additionalSpecifications).ToArray();
        var options = FindOptionsBuilder.Build<TEntity>(filter);

        return await DeleteSetAsync(context, specifications, options, cancellationToken).AnyContext();
    }
}