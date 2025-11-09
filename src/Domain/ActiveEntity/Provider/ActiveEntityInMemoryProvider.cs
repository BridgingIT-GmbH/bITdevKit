// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// In-memory implementation of IEntityProvider for testing purposes.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public class ActiveEntityInMemoryProvider<TEntity, TId> : IActiveEntityEntityProvider<TEntity, TId>
    where TEntity : class, IEntity
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ActiveEntityInMemoryProviderOptions<TEntity> options;
    private readonly InMemoryContext<TEntity> context;
    private readonly ReaderWriterLockSlim @lock = new();

    /// <summary>
    /// Initializes a new instance of the InMemoryProvider class.
    /// </summary>
    /// <param name="loggerFactory">The logger.</param>
    /// <param name="options">Configuration options for the in-memory provider.</param>
    public ActiveEntityInMemoryProvider(ILoggerFactory loggerFactory, ActiveEntityInMemoryProviderOptions<TEntity> options = null)
    {
        EnsureArg.IsNotNull(options, nameof(options));

        this.loggerFactory = loggerFactory;
        this.options = options;
        this.context = options.Context ?? new InMemoryContext<TEntity>();
        this.options.IdGenerator ??= new InMemoryEntityIdGenerator<TEntity>(this.context);
    }

    /// <summary>
    /// Initializes a new instance of the InMemoryProvider class using a builder.
    /// </summary>
    /// <param name="optionsBuilder">The builder for configuration options.</param>
    public ActiveEntityInMemoryProvider(ILoggerFactory loggerFactory, Builder<ActiveEntityInMemoryProviderOptionsBuilder<TEntity>, ActiveEntityInMemoryProviderOptions<TEntity>> optionsBuilder)
        : this(loggerFactory, optionsBuilder(new ActiveEntityInMemoryProviderOptionsBuilder<TEntity>()).Build()) { }

    /// <summary>
    /// Inserts an entity into the in-memory store.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the inserted entity.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
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
        if (entity == null)
        {
            return Result.Failure("Entity cannot be null.");
        }

        var result = await this.UpsertAsync(entity, callbacks, cancellationToken).AnyContext();

        return result.IsSuccess
            ? Result<TEntity>.Success(result.Value.entity)
            : Result<TEntity>.Failure().WithErrors(result.Errors);
    }

    /// <summary>
    /// Updates an existing entity in the in-memory store.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the updated entity.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var customer = await provider.FindOneAsync(customerId);
    /// customer.Value.FirstName = "Jane";
    /// var result = await provider.UpdateAsync(customer.Value);
    /// if (result.IsSuccess) { Console.WriteLine("Customer updated"); }
    /// </code>
    /// </example>
    public async Task<Result<TEntity>> UpdateAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        if (entity?.Id == null)
        {
            return Result.Failure("Entity or Id cannot be null.");
        }

        var result = await this.UpsertAsync(entity, callbacks, cancellationToken).AnyContext();

        return result.IsSuccess
            ? Result<TEntity>.Success(result.Value.entity)
            : Result<TEntity>.Failure().WithErrors(result.Errors);
    }

    /// <summary>
    /// Updates entities matching the given specification by setting the specified properties.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="set">A builder action to specify which properties to update and their values.</param>
    /// <param name="options">Optional find options (not used in in-memory provider).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the number of affected rows.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.UpdateAsync(
    ///     new Specification&lt;User&gt;(u => u.IsDeleted),
    ///     set => set.Set(u => u.Status, "Archived"));
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
    /// <param name="options">Optional find options (not used in in-memory provider).</param>
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
    public Task<Result<long>> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (set == null)
        {
            return Task.FromResult(Result<long>.Failure("Update set cannot be null."));
        }

        try
        {
            var updateBuilder = new EntityUpdateSet<TEntity>();
            set(updateBuilder);

            this.@lock.EnterWriteLock();
            try
            {
                var query = this.context.Entities.AsEnumerable();
                foreach (var spec in specifications.SafeNull())
                {
                    query = query.Where(spec.ToPredicate());
                }

                var entities = query.ToList();
                foreach (var entity in entities)
                {
                    updateBuilder.Apply(entity);
                }

                return Task.FromResult(Result<long>.Success(entities.Count));
            }
            finally
            {
                this.@lock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex)));
        }
    }

    /// <summary>
    /// Upserts an entity in the in-memory store (inserts if new, updates if exists).
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the entity and the action performed.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
    /// var result = await provider.UpsertAsync(customer);
    /// Console.WriteLine($"Action: {result.Value.action}"); // Inserted or Updated
    /// </code>
    /// </example>
    public async Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Result<(TEntity, RepositoryActionResult)>.Failure("Entity cannot be null.");
        }

        bool isNew;
        if (this.options.IdGenerator.IsNew(entity.Id))
        {
            this.options.IdGenerator.SetNew(entity);
            isNew = true;
        }
        else
        {
            isNew = !await this.ExistsAsync(entity.Id, cancellationToken).AnyContext();
        }

        this.@lock.EnterWriteLock();
        try
        {
            if (!isNew)
            {
                this.context.TryGet(entity.Id, out var existingEntity);

                if (existingEntity is IConcurrency existingConcurrency && entity is IConcurrency entityConcurrency && this.options.EnableOptimisticConcurrency)
                {
                    if (!existingConcurrency.ConcurrencyVersion.IsEmpty() && existingConcurrency.ConcurrencyVersion != entityConcurrency.ConcurrencyVersion)
                    {
                        return Result<(TEntity, RepositoryActionResult)>.Failure($"Concurrency conflict detected for entity {typeof(TEntity).Name} with Id {entity.Id}", new ConcurrencyError());
                        //.WithValue((default, RepositoryActionResult.None));
                    }
                }

                this.context.TryRemove(entity.Id, out _);
            }

            if (entity is IConcurrency concurrencyEntity)
            {
                concurrencyEntity.ConcurrencyVersion = GuidGenerator.CreateSequential();
            }

            if (callbacks?.BeforeUpsertAsync != null)
            {
                var cbResult = await callbacks.BeforeUpsertAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            if (!isNew)
            {
                if (callbacks?.BeforeUpdateAsync != null)
                {
                    var cbResult = await callbacks.BeforeUpdateAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }
            }
            else
            {
                if (callbacks?.BeforeInsertAsync != null)
                {
                    var cbResult = await callbacks.BeforeInsertAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }
            }

            this.context.TryAdd(entity.Clone()); // add a clone to avoid external modifications

            if (!isNew)
            {
                if (callbacks?.AfterUpdateAsync != null)
                {
                    var cbResult = await callbacks.AfterUpdateAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }
            }
            else
            {
                if (callbacks?.AfterInsertAsync != null)
                {
                    var cbResult = await callbacks.AfterInsertAsync(this, cancellationToken).AnyContext();
                    if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
                }
            }

            if (callbacks?.AfterUpsertAsync != null)
            {
                var cbResult = await callbacks.AfterUpsertAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }

        return Result<(TEntity, RepositoryActionResult)>.Success((entity, isNew ? RepositoryActionResult.Inserted : RepositoryActionResult.Updated));
    }

    /// <summary>
    /// Deletes an entity from the in-memory store.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var customer = await provider.FindOneAsync(customerId);
    /// var result = await provider.DeleteAsync(customer.Value);
    /// if (result.IsSuccess) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    public async Task<Result> DeleteAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Result.Failure("Entity cannot be null.");
        }

        this.@lock.EnterWriteLock();
        try
        {
            if (callbacks?.BeforeDeleteAsync != null)
            {
                var cbResult = await callbacks.BeforeDeleteAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            var result = await (this.context.TryRemove(entity.Id, out _)
                ? Task.FromResult(Result<RepositoryActionResult>.Success(RepositoryActionResult.Deleted))
                : Task.FromResult(Result<RepositoryActionResult>.Success(RepositoryActionResult.NotFound)));

            if (callbacks?.AfterDeleteAsync != null)
            {
                var cbResult = await callbacks.AfterDeleteAsync(this, cancellationToken).AnyContext();
                if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
            }

            return result;
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    ///// <summary>
    ///// Deletes an entity by its ID from the in-memory store.
    ///// </summary>
    ///// <param name="id">The ID of the entity to delete.</param>
    ///// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    ///// <returns>A task with a Result containing the action performed (Deleted/None/NotFound).</returns>
    ///// <example>
    ///// <code>
    ///// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    ///// var result = await provider.DeleteAsync(customerId);
    ///// if (result.IsSuccess && result.Value == RepositoryActionResult.Deleted) { Console.WriteLine("Customer deleted"); }
    ///// </code>
    ///// </example>
    //public async Task<Result<RepositoryActionResult>> DeleteAsync(
    //    object id,
    //    ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
    //    CancellationToken cancellationToken = default)
    //{
    //    if (id == null)
    //    {
    //        return await Task.FromResult(Result<RepositoryActionResult>.Failure("Id cannot be null."));//.WithValue(RepositoryActionResult.None);
    //    }

    //    this.@lock.EnterWriteLock();
    //    try
    //    {
    //        if (callbacks?.BeforeDeleteAsync != null)
    //        {
    //            var cbResult = await callbacks.BeforeDeleteAsync(cancellationToken).AnyContext();
    //            if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
    //        }

    //        var result = await (this.context.TryRemove(id, out _)
    //            ? Task.FromResult(Result<RepositoryActionResult>.Success(RepositoryActionResult.Deleted))
    //            : Task.FromResult(Result<RepositoryActionResult>.Success(RepositoryActionResult.NotFound)));

    //        if (callbacks?.AfterDeleteAsync != null)
    //        {
    //            var cbResult = await callbacks.AfterDeleteAsync(cancellationToken).AnyContext();
    //            if (cbResult.IsFailure) return Result.Failure().WithErrors(cbResult.Errors);
    //        }

    //        return result;
    //    }
    //    finally
    //    {
    //        this.@lock.ExitWriteLock();
    //    }
    //}

    /// <summary>
    /// Deletes entities matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (not used in in-memory provider).</param>
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
    /// <param name="options">Optional find options (not used in in-memory provider).</param>
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
    public Task<Result<long>> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this.@lock.EnterWriteLock();
            try
            {
                var query = this.context.Entities.AsEnumerable();
                foreach (var spec in specifications.SafeNull())
                {
                    query = query.Where(spec.ToPredicate());
                }

                var entities = query.ToList();
                foreach (var entity in entities)
                {
                    this.context.TryRemove(entity.Id, out _);
                }

                return Task.FromResult(Result<long>.Success(entities.Count));
            }
            finally
            {
                this.@lock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex)));
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var result = await provider.FindOneAsync(customerId);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    public Task<Result<TEntity>> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        if (id == null)
        {
            return Task.FromResult(Result<TEntity>.Failure("Id cannot be null."));
        }

        //this.@lock.EnterReadLock();
        //try
        //{
        this.context.TryGet(id, out var entity);

        return Task.FromResult(Result<TEntity>.SuccessIf(entity != null, entity, new NotFoundError()));
        //}
        //finally
        //{
        //    this.@lock.ExitReadLock();
        //}
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var options = new FindOptions<Customer> { Take = 10 };
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllAsync(specs);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    public Task<Result<IEnumerable<TEntity>>> FindAllAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        //this.@lock.EnterReadLock();
        //try
        //{
        var result = this.context.Entities.AsEnumerable();

        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(specification.ToPredicate());
        }

        var entities = this.FindAll(result, options, cancellationToken).ToList();

        return Task.FromResult(Result<IEnumerable<TEntity>>.Success(entities));
        //}
        //finally
        //{
        //    this.@lock.ExitReadLock();
        //}
    }

    /// <summary>
    /// Finds all entities matching the given options with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await provider.FindAllPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TEntity>> FindAllPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options: options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(cancellationToken: cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TEntity>.Success(entities.Value, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await provider.FindAllPagedAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TEntity>> FindAllPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specification: specification, cancellationToken: cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TEntity>.Success(entities.Value, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllPagedAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TEntity>> FindAllPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specifications: specifications, cancellationToken: cancellationToken).AnyContext();
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TEntity>.Success(entities.Value, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var result = await provider.ProjectAllAsync(c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options: options, cancellationToken).AnyContext();
        var compiledProjection = projection.Compile();
        var projected = entities.Value.Select(compiledProjection);

        return Result<IEnumerable<TProjection>>.Success(projected);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await provider.ProjectAllAsync(spec, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var compiledProjection = projection.Compile();
        var projected = entities.Value.Select(compiledProjection);

        return Result<IEnumerable<TProjection>>.Success(projected);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.ProjectAllAsync(specs, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var compiledProjection = projection.Compile();
        var projected = entities.Value.Select(compiledProjection);

        return Result<IEnumerable<TProjection>>.Success(projected);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await provider.ProjectAllPagedAsync(c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options: options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(cancellationToken: cancellationToken).AnyContext();
        var compiledProjection = projection.Compile();
        var projected = entities.Value.Select(compiledProjection);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TProjection>.Success(projected, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await provider.ProjectAllPagedAsync(spec, c => c.FirstName);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specification: specification, cancellationToken: cancellationToken).AnyContext();
        var compiledProjection = projection.Compile();
        var projected = entities.Value.Select(compiledProjection);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TProjection>.Success(projected, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.ProjectAllPagedAsync(specs, c => c.FirstName);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specifications: specifications, cancellationToken: cancellationToken).AnyContext();
        var compiledProjection = projection.Compile();
        var projected = entities.Value.Select(compiledProjection);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TProjection>.Success(projected, totalCount.Value, page ?? 1);
    }

    /// <summary>
    /// Checks if any entities exist matching the given options.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var result = await provider.ExistsAsync();
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.ExistsAsync(new Specification<TEntity>(e => e.Id.Equals(id)), null, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Checks if any entities exist matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
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
    /// Checks if any entities exist matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await provider.ExistsAsync(spec);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var count = await this.CountAsync(specification, cancellationToken: cancellationToken).AnyContext();

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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.ExistsAsync(specs);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    public async Task<Result<bool>> ExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var count = await this.CountAsync(specifications: specifications, cancellationToken: cancellationToken).AnyContext();

        return Result<bool>.Success(count.Value > 0);
    }

    /// <summary>
    /// Counts entities matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var result = await provider.CountAsync();
    /// if (result.IsSuccess) { Console.WriteLine($"Total entities: {result.Value}"); }
    /// </code>
    /// </example>
    public Task<Result<long>> CountAsync(CancellationToken cancellationToken = default)
    {
        //this.@lock.EnterReadLock();
        //try
        //{
        var result = this.context.Entities.AsEnumerable();
        result = this.FindAll(result, null, cancellationToken);

        return Task.FromResult(Result<long>.Success(result.Count()));
        //}
        //finally
        //{
        //    this.@lock.ExitReadLock();
        //}
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.CountAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    public Task<Result<long>> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        //this.@lock.EnterReadLock();
        //try
        //{
        var result = this.context.Entities.AsEnumerable();
        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(specification.ToPredicate());
        }

        result = this.FindAll(result, options, cancellationToken);

        return Task.FromResult(Result<long>.Success(result.Count()));
        //}
        //finally
        //{
        //    this.@lock.ExitReadLock();
        //}
    }

    /// <summary>
    /// Finds all entity IDs matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var result = await provider.FindAllIdsAsync();
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options: options, cancellationToken).AnyContext();
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await provider.FindAllIdsPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TId>> FindAllIdsPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(options: options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(cancellationToken: cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TId>.Success(ids, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await provider.FindAllIdsPagedAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TId>> FindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specification, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specification: specification, cancellationToken: cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TId>.Success(ids, totalCount.Value, page ?? 1);
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
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.FindAllIdsPagedAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    public async Task<ResultPaged<TId>> FindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).AnyContext();
        var totalCount = await this.CountAsync(specifications: specifications, cancellationToken: cancellationToken).AnyContext();
        var ids = entities.Value.Select(e => (TId)e.Id);
        var page = options?.Skip > 0 && options?.Take > 0 ? (options.Skip / options.Take) + 1 : 1;

        return ResultPaged<TId>.Success(ids, totalCount.Value, page ?? 1);
    }

    /// <summary>
    /// Begins a transaction in the in-memory store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the transaction object.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var result = await provider.BeginTransactionAsync();
    /// if (result.IsSuccess) { var transaction = result.Value; /* Use transaction */ }
    /// </code>
    /// </example>
    public Task<Result<IDatabaseTransaction>> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<IDatabaseTransaction>.Success());
    }

    /// <summary>
    /// Commits a transaction in the in-memory store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var transactionResult = await provider.BeginTransactionAsync();
    /// if (transactionResult.IsSuccess) { await provider.CommitAsync(); }
    /// </code>
    /// </example>
    public Task<Result> CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // In-memory provider does not require actual commit logic; transactions are simulated.
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Rolls back a transaction in the in-memory store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var provider = new InMemoryProvider<Customer, Guid>(loggerFactory, new InMemoryContext<Customer>());
    /// var transactionResult = await provider.BeginTransactionAsync();
    /// if (transactionResult.IsSuccess) { await provider.RollbackAsync(); }
    /// </code>
    /// </example>
    public Task<Result> RollbackAsync(CancellationToken cancellationToken = default)
    {
        // In-memory provider does not require actual rollback logic; transactions are simulated.
        return Task.FromResult(Result.Success());
    }

    private IEnumerable<TEntity> FindAll(IEnumerable<TEntity> entities, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        //this.@lock.EnterReadLock();
        //try
        //{
        var result = entities;

        if (options?.Distinct?.Expression != null)
        {
            result = result.GroupBy(options.Distinct.Expression.Compile()).Select(g => g.FirstOrDefault());
        }

        if (options?.Skip.HasValue == true && options.Skip.Value > 0)
        {
            result = result.Skip(options.Skip.Value);
        }

        if (options?.Take.HasValue == true && options.Take.Value > 0)
        {
            result = result.Take(options.Take.Value);
        }

        if (options?.Distinct != null && options.Distinct.Expression == null)
        {
            result = result.Distinct();
        }
        else if (options?.Distinct != null && options.Distinct.Expression != null)
        {
            result = result.GroupBy(options.Distinct.Expression.Compile()).Select(g => g.FirstOrDefault());
        }

        IOrderedEnumerable<TEntity> orderedResult = null;
        foreach (var order in (options?.Orders ?? []).Insert(options?.Order))
        {
            orderedResult = orderedResult == null
                ? order.Direction == OrderDirection.Ascending
                    ? result.OrderBy(order.Expression.Compile())
                    : result.OrderByDescending(order.Expression.Compile())
                : order.Direction == OrderDirection.Ascending
                    ? orderedResult.ThenBy(order.Expression.Compile())
                    : orderedResult.ThenByDescending(order.Expression.Compile());
        }

        if (orderedResult != null)
        {
            result = orderedResult;
        }

        return result;
        //}
        //finally
        //{
        //    this.@lock.ExitReadLock();
        //}
    }
}
