// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

public partial class EntityFrameworkActiveEntityProvider<TEntity, TId, TContext> : IActiveEntityEntityProvider<TEntity, TId>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    private readonly EntityFrameworkActiveEntityProviderOptions<TContext, TEntity> options;
    private readonly TContext context;

    public ILogger<EntityFrameworkActiveEntityProvider<TEntity, TId, TContext>> Logger { get; }

    /// <summary>
    /// Initializes a new instance of the EntityFrameworkProvider class.
    /// </summary>
    /// <param name="context">The DbContext instance to use for database operations.</param>
    /// <param name="options">Configuration options for the EF provider.</param>
    public EntityFrameworkActiveEntityProvider(
        TContext context,
        EntityFrameworkActiveEntityProviderOptions<TContext, TEntity> options = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
        this.options = options ?? new EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>();
        this.Logger = this.options.CreateLogger<EntityFrameworkActiveEntityProvider<TEntity, TId, TContext>>();
    }

    /// <summary>
    /// Initializes a new instance of the EntityFrameworkProvider class using a builder.
    /// </summary>
    /// <param name="context">The DbContext instance to use for database operations.</param>
    /// <param name="optionsBuilder">The builder for configuration options.</param>
    public EntityFrameworkActiveEntityProvider(
        TContext context,
        Builder<EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>, EntityFrameworkActiveEntityProviderOptions<TContext, TEntity>> optionsBuilder)
        : this(context, optionsBuilder(new EntityFrameworkActiveEntityProviderOptionsBuilder<TContext, TEntity>()).Build()) { }

    /// <summary>
    /// Inserts an entity into the database.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the inserted entity.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var customer = new Customer { FirstName = "John" };
    /// var result = await provider.InsertAsync(customer);
    /// if (result.IsSuccess) { Console.WriteLine($"Inserted customer with ID: {result.Value.Id}"); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> InsertAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, callbacks, cancellationToken).AnyContext(); // TODO: pass these functions here
        return result.IsSuccess && result.Value.action == RepositoryActionResult.Inserted
            ? Result<TEntity>.Success(result.Value.entity)
            : Result<TEntity>.Failure().WithErrors(result.Errors);
    }

    /// <summary>
    /// Updates an existing entity in the database.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the updated entity.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var customer = (await provider.FindOneAsync(customerId)).Value;
    /// customer.FirstName = "Jane";
    /// var result = await provider.UpdateAsync(customer);
    /// if (result.IsSuccess) { Console.WriteLine("Customer updated"); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> UpdateAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, callbacks, cancellationToken).AnyContext();
        return result.IsSuccess && result.Value.action == RepositoryActionResult.Updated
            ? Result<TEntity>.Success(result.Value.entity)
            : Result<TEntity>.Failure().WithErrors(result.Errors);
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
    /// var result = await provider.UpdateAsync(
    ///     new Specification&lt;User&gt;(u => u.LastLogin &lt; DateTime.UtcNow.AddYears(-1)),
    ///     set => set
    ///         .Set(u => u.IsActive, false)
    ///         .Set(u => u.Status, "Inactive"));
    /// if (result.IsSuccess) { Console.WriteLine($"Updated {result.Value} users"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.UpdateSetAsync([specification], set, options, cancellationToken);
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
    /// var result = await provider.UpdateAsync(
    ///     specs,
    ///     set => set.Set(u => u.Status, "Archived"));
    /// if (result.IsSuccess) { Console.WriteLine($"Archived {result.Value} users"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (set == null)
        {
            return Result.Failure("Update set cannot be null.");
        }

        try
        {
            var updateBuilder = new EntityFrameworkEntityUpdateSet<TEntity>(); // Build the update set using the provided builder action
            set(updateBuilder);

            var query = this.BuildQuery(this.context.Set<TEntity>(), options, specifications);

            // Create a parameter expression representing "setters"
            var parameter = Expression.Parameter(typeof(SetPropertyCalls<TEntity>), "setters");
            Expression body = parameter;

            // Apply each assignment (chained SetProperty calls)
            foreach (var apply in updateBuilder.Assignments)
            {
                body = apply(body);
            }

            // Build the final lambda expression: setters => setters.SetProperty(...).SetProperty(...).SetProperty(...)
            var lambda = Expression.Lambda<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>>(body, parameter);
            var affected = await query.ExecuteUpdateAsync(lambda, cancellationToken);

            return Result<long>.Success(affected);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Upserts an entity in the database (inserts if new, updates if exists).
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the entity and the action performed.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
    /// var result = await provider.UpsertAsync(customer);
    /// Console.WriteLine($"Action: {result.Value.action}");
    /// </code>
    /// </example>
    public async Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default) // TODO: accept before/after functions for custom logic for insert and update and upsert -> pass them as an options instance or so
    {
        if (entity is null)
        {
            return Result<(TEntity, RepositoryActionResult)>.Failure("Entity cannot be null.");
        }

        var isNew = this.IsDefaultId(entity.Id);

        try
        {
            if (!isNew) // Check existence if ID is set, could still be isnew if not found
            {
                var existingEntity = await this.context.FindAsync(entity.Id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext();
                isNew = existingEntity is null;
            }

            if (callbacks?.BeforeUpsertAsync != null)
            {
                var cbResult = await callbacks.BeforeUpsertAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            if (isNew)
            {
                if (callbacks?.BeforeInsertAsync != null)
                {
                    var cbResult = await callbacks.BeforeInsertAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }

                await this.HandleInsertAsync(entity, cancellationToken);

                if (callbacks?.AfterInsertAsync != null)
                {
                    var cbResult = await callbacks.AfterInsertAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }
            }
            else
            {
                if (callbacks?.BeforeUpdateAsync != null)
                {
                    var cbResult = await callbacks.BeforeUpdateAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }

                await this.HandleUpdateAsync(entity, cancellationToken);

                if (callbacks?.AfterUpdateAsync != null)
                {
                    var cbResult = await callbacks.AfterUpdateAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }
            }

            if (callbacks?.AfterUpsertAsync != null)
            {
                var cbResult = await callbacks.AfterUpsertAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            foreach (var entry in this.context.ChangeTracker.Entries())
            {
                TypedLogger.LogEntityState(this.Logger, BridgingIT.DevKit.Infrastructure.EntityFramework.Constants.LogKey, entry.Entity.GetType().Name, entry.IsKeySet, entry.State);
            }

            await this.context.SaveChangesAsync(cancellationToken).AnyContext();

            return Result<(TEntity, RepositoryActionResult)>.Success((entity, isNew ? RepositoryActionResult.Inserted : RepositoryActionResult.Updated));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Result<(TEntity, RepositoryActionResult)>.Failure($"Concurrency conflict detected for entity {typeof(TEntity).Name} with Id {entity.Id}", new ConcurrencyError(ex.Message));
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<(TEntity, RepositoryActionResult)>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Deletes an entity from the database.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var customer = (await provider.FindOneAsync(customerId)).Value;
    /// var result = await provider.DeleteAsync(customer);
    /// if (result.IsSuccess) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public async Task<Result> DeleteAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        if (entity?.Id == null)
        {
            return Result.Failure("Entity or Id cannot be null.");
        }

        try
        {
            TypedLogger.LogDelete(this.Logger, Constants.LogKey, this.context.ContextId.ToString(), this.context.GetType().Name, typeof(TEntity).Name, entity.Id);

            if (callbacks?.BeforeDeleteAsync != null)
            {
                var cbResult = await callbacks.BeforeDeleteAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            this.context.Set<TEntity>().Remove(entity);
            await this.context.SaveChangesAsync(cancellationToken).AnyContext();

            if (callbacks?.AfterDeleteAsync != null)
            {
                var cbResult = await callbacks.AfterDeleteAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            return Result<RepositoryActionResult>.Success(RepositoryActionResult.Deleted);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<RepositoryActionResult>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Deletes an entity by its ID from the database.
    /// </summary>
    /// <param name="id">The ID of the entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the action performed (Deleted/None/NotFound).</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.DeleteAsync(customerId);
    /// if (result.IsSuccess && result.Value == RepositoryActionResult.Deleted) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public async Task<Result<RepositoryActionResult>> DeleteAsync(
        object id,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        if (id is null)
        {
            return Result<RepositoryActionResult>.Failure("Id cannot be null.");
        }

        try
        {
            TypedLogger.LogDelete(this.Logger, Constants.LogKey, this.context.ContextId.ToString(), this.context.GetType().Name, typeof(TEntity).Name, id);
            var entity = await this.context.FindAsync(this.ConvertEntityId(id), new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext();
            if (entity is null)
            {
                return Result<RepositoryActionResult>.Failure(RepositoryActionResult.NotFound).WithError(new NotFoundError("entity not found"));
            }

            if (callbacks?.BeforeDeleteAsync != null)
            {
                var cbResult = await callbacks.BeforeDeleteAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            this.context.Set<TEntity>().Remove(entity);
            await this.context.SaveChangesAsync(cancellationToken).AnyContext();

            if (callbacks?.AfterDeleteAsync != null)
            {
                var cbResult = await callbacks.AfterDeleteAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            return Result<RepositoryActionResult>.Success(RepositoryActionResult.Deleted);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<RepositoryActionResult>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
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
    /// var result = await provider.DeleteAsync(
    ///     new Specification&lt;User&gt;(u => u.IsDeleted));
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.DeleteSetAsync([specification], options, cancellationToken);
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
    /// var result = await provider.DeleteAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Deleted {result.Value} users"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = this.BuildQuery(this.context.Set<TEntity>(), options, specifications);
            var affected = await query.ExecuteDeleteAsync(cancellationToken);

            return Result<long>.Success(affected);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Finds a single entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.FindOneAsync(customerId);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        if (id is null)
        {
            return Result<TEntity>.Failure("Id cannot be null.");
        }

        try
        {
            var entity = await this.context.FindAsync(this.ConvertEntityId(id), options, cancellationToken).AnyContext();
            return Result<TEntity>.SuccessIf(entity != null, entity, new NotFoundError());
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<TEntity>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.FindOneAsync(spec);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> FindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entity = (await this.FindAllAsync([specification], options, cancellationToken).AnyContext()).Value.FirstOrDefault();

        return Result<TEntity>.SuccessIf(entity != null, entity, new NotFoundError());
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.FindOneAsync(specs);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> FindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entity = (await this.FindAllAsync(specifications, options, cancellationToken).AnyContext()).Value.FirstOrDefault();

        return Result<TEntity>.SuccessIf(entity != null, entity, new NotFoundError());
    }

    /// <summary>
    /// Finds all entities matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var options = new FindOptions&lt;Customer&gt; { Take = 10 };
    /// var result = await provider.FindAllAsync(options);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TEntity>>> FindAllAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(specifications: null, options, cancellationToken).AnyContext();
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.FindAllAsync(spec);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TEntity>>> FindAllAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllAsync(specs);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TEntity>>> FindAllAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = this.BuildQuery(this.context.Set<TEntity>(), options, specifications);
            var entities = await query
                .ToListAsync(cancellationToken).AnyContext();
            return Result<IEnumerable<TEntity>>.Success(entities);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TEntity>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Finds all entities matching the given options with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var options = new FindOptions&lt;Customer&gt; { Skip = 10, Take = 5 };
    /// var result = await provider.FindAllPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TEntity>> FindAllPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(options, cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TEntity>.Success(entities.Value, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.FindAllPagedAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TEntity>> FindAllPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specification, options, cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TEntity>.Success(entities.Value, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllPagedAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TEntity>> FindAllPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specifications, options, cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TEntity>.Success(entities.Value, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
    }

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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.ProjectAllAsync(c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = this.BuildQuery(this.context.Set<TEntity>(), options);
            var projected = await query
                .Select(projection)
                .ToListAsync(cancellationToken).AnyContext();
            return Result<IEnumerable<TProjection>>.Success(projected);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TProjection>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.ProjectAllAsync(spec, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([specification], projection, options, cancellationToken).AnyContext();
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.ProjectAllAsync(specs, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = this.BuildQuery(this.context.Set<TEntity>(), options, specifications);
            var projected = await query
                .Select(projection)
                .ToListAsync(cancellationToken).AnyContext();
            return Result<IEnumerable<TProjection>>.Success(projected);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IEnumerable<TProjection>>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var options = new FindOptions&lt;Customer&gt; { Skip = 10, Take = 5 };
    /// var result = await provider.ProjectAllPagedAsync(c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(options, cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TProjection>.Success(entities.Value, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.ProjectAllPagedAsync(spec, c => c.FirstName);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specification, options, cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TProjection>.Success(entities.Value, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.ProjectAllPagedAsync(specs, c => c.FirstName);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specifications, options, cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TProjection>.Success(entities.Value, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
    }

    /// <summary>
    /// Checks if any entities exist matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.ExistsAsync();
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken = default)
    {
        var count = await this.CountAsync(cancellationToken: cancellationToken).AnyContext();

        return Result<bool>.Success(count.Value > 0);
    }

    /// <summary>
    /// Checks if an entity exists by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if the entity exists.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.ExistsAsync(customerId);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entity exists"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id is null)
        {
            return Result<bool>.Failure("Id cannot be null.");
        }

        try
        {
            var entity = await this.context.FindAsync(this.ConvertEntityId(id), new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext();
            return Result<bool>.Success(entity != null);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<bool>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Checks if any entities exist matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.ExistsAsync(spec);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var count = await this.CountAsync(specification, options, cancellationToken).AnyContext();

        return Result<bool>.Success(count.Value > 0);
    }

    /// <summary>
    /// Checks if any entities exist matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.ExistsAsync(specs);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var count = await this.CountAsync(specifications, options, cancellationToken).AnyContext();

        return Result<bool>.Success(count.Value > 0);
    }

    /// <summary>
    /// Counts entities matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.CountAsync();
    /// if (result.IsSuccess) { Console.WriteLine($"Total entities: {result.Value}"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(null, cancellationToken).AnyContext();
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.CountAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> CountAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], options, cancellationToken).AnyContext();
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.CountAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public async Task<Result<long>> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = this.BuildQuery(this.context.Set<TEntity>(), options, specifications);
            var count = await query.LongCountAsync(cancellationToken).AnyContext();

            return Result<long>.Success(count);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Finds all entity IDs matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.FindAllIdsAsync();
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options, cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);

        return Result<IEnumerable<TId>>.Success(ids);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.FindAllIdsAsync(spec);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);

        return Result<IEnumerable<TId>>.Success(ids);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllIdsAsync(specs);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);

        return Result<IEnumerable<TId>>.Success(ids);
    }

    /// <summary>
    /// Finds all entity IDs matching the given options with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var options = new FindOptions&lt;Customer&gt; { Skip = 10, Take = 5 };
    /// var result = await provider.FindAllIdsPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TId>> FindAllIdsPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(options, cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TId>.Success(ids, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var spec = new Specification&lt;Customer&gt;(c => c.LastName == "Doe");
    /// var result = await provider.FindAllIdsPagedAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TId>> FindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specification, options, cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TId>.Success(ids, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
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
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var specs = new[] { new Specification&lt;Customer&gt;(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllIdsPagedAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TId>> FindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specifications, options, cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TId>.Success(ids, totalCount.Value, page.Value, options?.Take > 0 ? options.Take.Value : 10);
    }

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the transaction object.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var result = await provider.BeginTransactionAsync();
    /// if (result.IsSuccess) { var transaction = result.Value; /* Use transaction */ }
    /// </code>
    /// </example>
    public async Task<Result<IDatabaseTransaction>> BeginTransactionAsync(CancellationToken cancellationToken = default) // begins a new transaction for the current dbcontext in this provider instance
    {
        try
        {
            if (this.context.Database.CurrentTransaction is null)
            {
                await this.context.Database.BeginTransactionAsync(cancellationToken).AnyContext();
            }

            return Result<IDatabaseTransaction>.Success(new DatabaseTransaction(this.context)).WithMessage("Transaction has been started.");
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<IDatabaseTransaction>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Commits a database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var transactionResult = await provider.BeginTransactionAsync();
    /// if (transactionResult.IsSuccess) { await provider.CommitAsync(); }
    /// </code>
    /// </example>
    public async Task<Result> CommitTransactionAsync(CancellationToken cancellationToken = default) // commits the transaction for the current dbcontext in this provider instance
    {
        try
        {
            await this.context.SaveChangesAsync(cancellationToken).AnyContext();

            if (this.context.Database.CurrentTransaction != null)
            {
                await this.context.Database.CommitTransactionAsync(cancellationToken).AnyContext();
            }
            else
            {
                return Result.Failure("No active transaction to commit.");
            }

            return Result.Success().WithMessage("Transaction has been commited.");
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Rolls back a database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var provider = new EntityFrameworkProvider&lt;Customer, Guid, AppDbContext&gt;(context);
    /// var transactionResult = await provider.BeginTransactionAsync();
    /// if (transactionResult.IsSuccess) { await provider.RollbackAsync(); }
    /// </code>
    /// </example>
    public async Task<Result> RollbackAsync(CancellationToken cancellationToken = default) // rolls back the transaction for the current dbcontext in this provider instance
    {
        try
        {
            if (this.context.Database.CurrentTransaction != null)
            {
                await this.context.Database.RollbackTransactionAsync(cancellationToken).AnyContext();
            }
            else
            {
                return Result.Failure("No active transaction to rollback.");
            }

            return Result.Success().WithMessage("Transaction has been rolled back.");
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    private async Task<Result<long>> CountAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = this.BuildQuery(this.context.Set<TEntity>(), options);
            var count = await query.LongCountAsync(cancellationToken).AnyContext();
            return Result<long>.Success(count);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    private Task HandleInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogUpsert(this.Logger, Constants.LogKey, "insert", this.context.GetType().Name, this.context.ContextId.ToString(), typeof(TEntity).Name, entity.Id, false);

        if (entity is IConcurrency concurrencyEntity)
        {
            concurrencyEntity.ConcurrencyVersion = this.options.VersionGenerator();
        }

        //context.Set<TEntity>().Add(entity); // causes re-inserts of existing child entities
        this.context.Update(entity);

        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var isTracked = this.context.ChangeTracker.Entries<TEntity>().Any(e => Equals(e.Entity.Id, entity.Id));

        TypedLogger.LogUpsert(this.Logger, Constants.LogKey, "update", this.context.GetType().Name, this.context.ContextId.ToString(), typeof(TEntity).Name, entity.Id, isTracked);

        if (this.options.MergeStrategy != null)
        {
            var mergedEntity = await this.options.MergeStrategy(this.context, entity, cancellationToken);
            if (mergedEntity is IConcurrency concurrencyEntity && this.options.EnableOptimisticConcurrency)
            {
                var originalVersion = concurrencyEntity.ConcurrencyVersion;
                concurrencyEntity.ConcurrencyVersion = this.options.VersionGenerator();

                this.context.Entry(mergedEntity).Property(nameof(IConcurrency.ConcurrencyVersion)).OriginalValue = originalVersion;
            }

            this.context.Update(mergedEntity);
            return;
        }

        if (entity is IConcurrency concurrencyEntityDefault && this.options.EnableOptimisticConcurrency)
        {
            var originalVersion = concurrencyEntityDefault.ConcurrencyVersion;
            concurrencyEntityDefault.ConcurrencyVersion = this.options.VersionGenerator();

            if (isTracked)
            {
                this.context.Entry(entity).Property(nameof(IConcurrency.ConcurrencyVersion)).OriginalValue = originalVersion;
            }
            else
            {
                this.context.Update(entity);
                this.context.Entry(entity).Property(nameof(IConcurrency.ConcurrencyVersion)).OriginalValue = originalVersion;
            }
        }
        else if (!isTracked)
        {
            this.context.Update(entity);
        }
    }

    private bool IsDefaultId(object id)
    {
        if (id is null)
        {
            return true;
        }

        var idType = id.GetType();
        return idType switch
        {
            Type t when t == typeof(Guid) => (Guid)id == Guid.Empty,
            Type t when t == typeof(int) => (int)id == 0,
            Type t when t == typeof(long) => (long)id == 0,
            Type t when t == typeof(string) => string.IsNullOrEmpty((string)id),
            Type t when typeof(EntityId<Guid>).IsAssignableFrom(t) => ((EntityId<Guid>)id).Value == Guid.Empty,
            Type t when typeof(EntityId<int>).IsAssignableFrom(t) => ((EntityId<int>)id).Value == 0,
            Type t when typeof(EntityId<long>).IsAssignableFrom(t) => ((EntityId<long>)id).Value == 0,
            Type t when typeof(EntityId<string>).IsAssignableFrom(t) => string.IsNullOrEmpty(((EntityId<string>)id).Value),
            _ => Equals(id, Activator.CreateInstance(idType))
        };
    }

    private object ConvertEntityId(object value)
    {
        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(Guid) && value?.GetType() == typeof(string))
        {
            return Guid.Parse(value.ToString());
        }

        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(int) && value?.GetType() == typeof(string))
        {
            return int.Parse(value.ToString());
        }

        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(long) && value?.GetType() == typeof(string))
        {
            return long.Parse(value.ToString());
        }

        return value;
    }

    private IQueryable<TEntity> BuildQuery(IQueryable<TEntity> query, IFindOptions<TEntity> options, IEnumerable<ISpecification<TEntity>> specifications = null)
    {
        if (specifications != null)
        {
            foreach (var specification in specifications.SafeNull())
            {
                query = query.Where(specification.ToExpression());
            }
        }

        if (options?.NoTracking == true)
        {
            query = query.AsNoTracking();
        }

        if (options?.Distinct?.Expression != null)
        {
            query = query.GroupBy(options.Distinct.Expression).Select(g => g.FirstOrDefault());
        }
        else if (options?.Distinct != null)
        {
            query = query.Distinct();
        }

        if (options?.Skip.HasValue == true && options.Skip.Value > 0)
        {
            query = query.Skip(options.Skip.Value);
        }

        if (options?.Take.HasValue == true && options.Take.Value > 0)
        {
            query = query.Take(options.Take.Value);
        }

        foreach (var include in (options?.Includes ?? []).Insert(options?.Include))
        {
            query = query.Include(include.Expression);
        }

        IOrderedQueryable<TEntity> orderedQuery = null;
        foreach (var order in (options?.Orders ?? []).Insert(options?.Order))
        {
            orderedQuery = orderedQuery is null
                ? order.Direction == OrderDirection.Ascending
                    ? query.OrderBy(order.Expression)
                    : query.OrderByDescending(order.Expression)
                : order.Direction == OrderDirection.Ascending
                    ? orderedQuery.ThenBy(order.Expression)
                    : orderedQuery.ThenByDescending(order.Expression);
        }

        return orderedQuery ?? query;
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} active entity: upsert - {EntityUpsertType} (context={DbContextType}/{DbContextId}, type={EntityType}, id={EntityId}, tracked={EntityTracked})")]
        public static partial void LogUpsert(ILogger logger, string logKey, string entityUpsertType, string dbContextType, string dbContextId, string entityType, object entityId, bool entityTracked);

        [LoggerMessage(1, LogLevel.Debug, "{LogKey} active entity: delete (context={DbContextType}/{DbContextId}, type={EntityType}, id={EntityId})")]
        public static partial void LogDelete(ILogger logger, string logKey, string dbContextType, string dbContextId, string entityType, object entityId);

        [LoggerMessage(2, LogLevel.Trace, "{LogKey} dbcontext entity state: {EntityType} (keySet={EntityKeySet}) -> {EntityEntryState}")]
        public static partial void LogEntityState(ILogger logger, string logKey, string entityType, bool entityKeySet, EntityState entityEntryState);
    }
}