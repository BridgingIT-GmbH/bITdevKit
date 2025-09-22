// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the contract for behaviors in the Active Entities, providing hooks for operations like CRUD and queries.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IActiveEntityBehavior<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Executes before an entity is inserted.
    /// </summary>
    /// <param name="entity">The entity to be inserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeInsertAsync(T entity, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Preparing to insert {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an entity is inserted.
    /// </summary>
    /// <param name="entity">The inserted entity.</param>
    /// <param name="success">Indicates if the insert was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterInsertAsync(T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Insert of {typeof(T).Name} {(success ? "succeeded" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterInsertAsync(TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before an entity is updated.
    /// </summary>
    /// <param name="entity">The entity to be updated.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeUpdateAsync(T entity, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Preparing to update {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an entity is updated.
    /// </summary>
    /// <param name="entity">The updated entity.</param>
    /// <param name="success">Indicates if the update was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterUpdateAsync(T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Update of {typeof(T).Name} {(success ? "succeeded" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterUpdateAsync(TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before a set is updated. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result> BeforeUpdateSetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an set is updated. No-op by default.
    /// </summary>
    /// <param name="success">Indicates if the update was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result> AfterUpdateSetAsync(bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before an entity is upserted.
    /// </summary>
    /// <param name="entity">The entity to be upserted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeUpsertAsync(T entity, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Preparing to upsert {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an entity is upserted.
    /// </summary>
    /// <param name="entity">The upserted entity.</param>
    /// <param name="action">The action performed (Inserted/Updated).</param>
    /// <param name="success">Indicates if the upsert was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterUpsertAsync(T entity, RepositoryActionResult action, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Upsert of {typeof(T).Name} {action} {(success ? "succeeded" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterUpsertAsync(TEntity entity, RepositoryActionResult action, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before an entity is deleted.
    /// </summary>
    /// <param name="entity">The entity to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeDeleteAsync(T entity, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Preparing to delete {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an entity is deleted.
    /// </summary>
    /// <param name="entity">The deleted entity.</param>
    /// <param name="success">Indicates if the delete was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterDeleteAsync(T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Delete of {typeof(T).Name} {(success ? "succeeded" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterDeleteAsync(TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before an entity is deleted by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeDeleteAsync(object id, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Preparing to delete {typeof(T).Name} with ID {id}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeDeleteAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an entity is deleted by its ID.
    /// </summary>
    /// <param name="id">The ID of the deleted entity.</param>
    /// <param name="success">Indicates if the delete was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterDeleteAsync(object id, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Delete of {typeof(T).Name} with ID {id} {(success ? "succeeded" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterDeleteAsync(object id, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before a set is deleted. No-op by default.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result> BeforeDeleteSetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after an set is deleted. No-op by default.
    /// </summary>
    /// <param name="success">Indicates if the update was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<Result> AfterDeleteSetAsync(bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding a single entity by ID.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindOneAsync(object id, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding {typeof(T).Name} with ID {id}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindOneAsync(object id, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding a single entity by ID.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindOneAsync(object id, IFindOptions<T> options, T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {typeof(T).Name} with ID {id}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindOneAsync(object id, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding a single entity by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindOneAsync(ISpecification<T> specification, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding a single entity by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindOneAsync(ISpecification<T> specification, IFindOptions<T> options, T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding a single entity by a collection of specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindOneAsync(IEnumerable<ISpecification<T>> specifications, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding {typeof(T).Name} with specifications");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding a single entity by a collection of specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindOneAsync(IEnumerable<ISpecification<T>> specifications, IFindOptions<T> options, T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {typeof(T).Name} with specifications: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding a single entity by a filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindOneAsync(FilterModel filter, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding {typeof(T).Name} with filter");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding a single entity by a filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="entity">The found entity or null.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindOneAsync(FilterModel filter, IFindOptions<T> options, T entity, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {typeof(T).Name} with filter: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entities.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllAsync(IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding all {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entities.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The found entities.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllAsync(IFindOptions<T> options, IEnumerable<T> entities, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {entities.Count()} {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllAsync(IFindOptions<TEntity> options, IEnumerable<TEntity> entities, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entities by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAsync(ISpecification<T> specification, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entities by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The found entities.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAsync(ISpecification<T> specification, IFindOptions<T> options, IEnumerable<T> entities, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {entities.Count()} {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, IEnumerable<TEntity> entities, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entities with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllPagedAsync(IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding paged {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entities with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entities and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllPagedAsync(IFindOptions<T> options, ResultPaged<T> result, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found paged {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllPagedAsync(IFindOptions<TEntity> options, ResultPaged<TEntity> result, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before projecting all entities.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeProjectAllAsync<TProjection>(Expression<Func<T, TProjection>> projection, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Projecting all {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after projecting all entities.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The projected entities.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterProjectAllAsync<TProjection>(Expression<Func<T, TProjection>> projection, IFindOptions<T> options, IEnumerable<TProjection> entities, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Projected {entities.Count()} {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before projecting all entities by specification.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeProjectAllAsync<TProjection>(ISpecification<T> specification, Expression<Func<T, TProjection>> projection, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Projecting all {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after projecting all entities by specification.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="entities">The projected entities.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterProjectAllAsync<TProjection>(ISpecification<T> specification, Expression<Func<T, TProjection>> projection, IFindOptions<T> options, IEnumerable<TProjection> entities, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Projected {entities.Count()} {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, IEnumerable<TProjection> entities, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before projecting all entities with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeProjectAllPagedAsync<TProjection>(Expression<Func<T, TProjection>> projection, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Projecting paged {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after projecting all entities with pagination.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to.</typeparam>
    /// <param name="projection">The projection expression.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing projected entities and count.</param>
    /// <param name="success">Indicates if the projection was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterProjectAllPagedAsync<TProjection>(Expression<Func<T, TProjection>> projection, IFindOptions<T> options, ResultPaged<TProjection> result, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Projected paged {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, ResultPaged<TProjection> result, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before checking if entities exist.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeExistsAsync(IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Checking existence of {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after checking if entities exist.
    /// </summary>
    /// <param name="exists">Indicates if entities exist.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterExistsAsync(IFindOptions<T> options, bool exists, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Existence check for {typeof(T).Name}: {(exists ? "found" : "not found")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterExistsAsync(bool exists, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before checking if an entity exists by ID.
    /// </summary>
    /// <param name="id">The ID of the entity to check.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeExistsAsync(object id, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Checking existence of {typeof(T).Name} with ID {id}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeExistsAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after checking if an entity exists by ID.
    /// </summary>
    /// <param name="id">The ID of the entity to check.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterExistsAsync(object id, bool exists, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Existence check for {typeof(T).Name} with ID {id}: {(exists ? "found" : "not found")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterExistsAsync(object id, bool exists, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before checking if an entity exists by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeExistsAsync(ISpecification<T> specification, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Checking existence of {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeExistsAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after checking if an entity exists by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterExistsAsync(ISpecification<T> specification, bool exists, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Existence check for {typeof(T).Name} with specification {specification}: {(exists ? "found" : "not found")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterExistsAsync(ISpecification<TEntity> specification, bool exists, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before checking if an entity exists by a collection of specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeExistsAsync(IEnumerable<ISpecification<T>> specifications, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Checking existence of {typeof(T).Name} with specifications");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after checking if an entity exists by a collection of specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterExistsAsync(IEnumerable<ISpecification<T>> specifications, bool exists, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Existence check for {typeof(T).Name} with specifications: {(exists ? "found" : "not found")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, bool exists, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before checking if an entity exists by a filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeExistsAsync(FilterModel filter, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Checking existence of {typeof(T).Name} with filter");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeExistsAsync(FilterModel filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after checking if an entity exists by a filter model.
    /// </summary>
    /// <param name="filter">The filter model containing query criteria.</param>
    /// <param name="exists">Indicates if the entity exists.</param>
    /// <param name="success">Indicates if the check was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterExistsAsync(FilterModel filter, bool exists, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Existence check for {typeof(T).Name} with filter: {(exists ? "found" : "not found")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterExistsAsync(FilterModel filter, bool exists, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before counting entities.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeCountAsync(IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Counting {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after counting entities.
    /// </summary>
    /// <param name="count">The count of entities.</param>
    /// <param name="success">Indicates if the count was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterCountAsync(IFindOptions<T> options, long count, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Counted {count} {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterCountAsync(long count, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before counting entities by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeCountAsync(ISpecification<T> specification, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Counting {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeCountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after counting entities by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="count">The count of entities.</param>
    /// <param name="success">Indicates if the count was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterCountAsync(ISpecification<T> specification, long count, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Counted {count} {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterCountAsync(ISpecification<TEntity> specification, long count, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsAsync(IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding all IDs for {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsAsync(IFindOptions<T> options, IEnumerable<TId> ids, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {ids.Count()} IDs for {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsAsync<TId>(IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsAsync(ISpecification<T> specification, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding all IDs for {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs by specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsAsync(ISpecification<T> specification, IFindOptions<T> options, IEnumerable<TId> ids, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {ids.Count()} IDs for {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsAsync<TId>(ISpecification<TEntity> specification, IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs by specification.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsAsync(ISpecification<T> specification, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding all IDs for {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs by specification.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsAsync(ISpecification<T> specification, IFindOptions<T> options, IEnumerable<TId> ids, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {ids.Count()} IDs for {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsAsync<TId>(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs by specification.
    /// </summary>
    /// <param name="filter">The filter for the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsAsync(ISpecification<T> specification, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding all IDs for {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs by specification.
    /// </summary>
    /// <param name="filter">The filter for the entity.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="ids">The found entity IDs.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsAsync(ISpecification<T> specification, IFindOptions<T> options, IEnumerable<TId> ids, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found {ids.Count()} IDs for {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsAsync<TId>(FilterModel filter, IFindOptions<TEntity> options, IEnumerable<TId> ids, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsPagedAsync(IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding paged IDs for {typeof(T).Name}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsPagedAsync(IFindOptions<T> options, ResultPaged<TId> result, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found paged IDs for {typeof(T).Name}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsPagedAsync<TId>(IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs by specification with pagination.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsPagedAsync(ISpecification<T> specification, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding paged IDs for {typeof(T).Name} with specification {specification}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs by specification with pagination.
    /// </summary>
    /// <param name="specification">The specification to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsPagedAsync(ISpecification<T> specification, IFindOptions<T> options, ResultPaged<TId> result, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found paged IDs for {typeof(T).Name} with specification {specification}: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsPagedAsync<TId>(ISpecification<TEntity> specification, IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs by a collection of specifications with pagination.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsPagedAsync(IEnumerable<ISpecification<T>> specifications, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding paged IDs for {typeof(T).Name} with specifications");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs by a collection of specifications with pagination.
    /// </summary>
    /// <param name="specifications">The specifications to filter the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsPagedAsync(IEnumerable<ISpecification<T>> specifications, IFindOptions<T> options, ResultPaged<TId> result, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found paged IDs for {typeof(T).Name} with specifications: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsPagedAsync<TId>(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before finding all entity IDs by a collection of specifications with pagination.
    /// </summary>
    /// <param name="filter">The filter for the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeFindAllIdsPagedAsync(IEnumerable<ISpecification<T>> specifications, IFindOptions<T> options, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Finding paged IDs for {typeof(T).Name} with specifications");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeFindAllIdsPagedAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after finding all entity IDs by a collection of specifications with pagination.
    /// </summary>
    /// <param name="filter">The filter for the entity.</param>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="result">The paged result containing entity IDs and count.</param>
    /// <param name="success">Indicates if the find was successful.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterFindAllIdsPagedAsync(IEnumerable<ISpecification<T>> specifications, IFindOptions<T> options, ResultPaged<TId> result, bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Found paged IDs for {typeof(T).Name} with specifications: {(success ? "success" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterFindAllIdsPagedAsync<TId>(FilterModel filter, IFindOptions<TEntity> options, ResultPaged<TId> result, bool success, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes before a transaction is started.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task BeforeTransactionAsync(CancellationToken ct)
    /// {
    ///     Console.WriteLine("Starting transaction");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> BeforeTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after a transaction is completed (committed or rolled back).
    /// </summary>
    /// <param name="success">Indicates if the transaction was successful (committed).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// public override async Task AfterTransactionAsync(bool success, CancellationToken ct)
    /// {
    ///     Console.WriteLine($"Transaction {(success ? "succeeded" : "failed")}");
    ///     await Task.CompletedTask;
    /// }
    /// </code>
    /// </example>
    Task<Result> AfterTransactionAsync(bool success, CancellationToken cancellationToken = default);
}
