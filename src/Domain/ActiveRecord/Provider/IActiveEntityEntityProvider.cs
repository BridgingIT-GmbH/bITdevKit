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
/// Defines the contract for a persistence provider in the Active Entity pattern, handling CRUD, queries, and transactions for entities.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public interface IActiveEntityEntityProvider<TEntity, TId>
    //where TEntity : ActiveEntity<TEntity, TId>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Inserts an entity into the underlying storage.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the inserted entity.</returns>
    /// <example>
    /// <code>
    /// var customer = new Customer { FirstName = "John", LastName = "Doe" };
    /// var result = await provider.InsertAsync(customer);
    /// if (result.IsSuccess) { Console.WriteLine($"Inserted customer with ID: {result.Value.Id}"); }
    /// </code>
    /// </example>
    Task<Result<TEntity>> InsertAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the underlying storage.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the updated entity.</returns>
    /// <example>
    /// <code>
    /// var customer = await provider.FindOneAsync(customerId);
    /// customer.Value.FirstName = "Jane";
    /// var result = await provider.UpdateAsync(customer.Value);
    /// if (result.IsSuccess) { Console.WriteLine("Customer updated"); }
    /// </code>
    /// </example>
    Task<Result<TEntity>> UpdateAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default);

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
    Task<Result<long>> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

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
    Task<Result<long>> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts an entity (inserts if new, updates if exists) in the underlying storage.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the entity and the action performed (Inserted/Updated).</returns>
    /// <example>
    /// <code>
    /// var customer = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
    /// var result = await provider.UpsertAsync(customer);
    /// Console.WriteLine($"Action: {result.Value.action}"); // Inserted or Updated
    /// </code>
    /// </example>
    Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity from the underlying storage.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var customer = await provider.FindOneAsync(customerId);
    /// var result = await provider.DeleteAsync(customer.Value);
    /// if (result.IsSuccess) { Console.WriteLine("Customer deleted"); }
    /// </code>
    /// </example>
    Task<Result> DeleteAsync(
        TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
        CancellationToken cancellationToken = default);

    ///// <summary>
    ///// Deletes an entity by its ID from the underlying storage.
    ///// </summary>
    ///// <param name="id">The ID of the entity to delete.</param>
    ///// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    ///// <returns>A task with a Result containing the action performed (Deleted/None/NotFound).</returns>
    ///// <example>
    ///// <code>
    ///// var result = await provider.DeleteAsync(customerId);
    ///// if (result.IsSuccess && result.Value == RepositoryActionResult.Deleted) { Console.WriteLine("Customer deleted"); }
    ///// </code>
    ///// </example>
    //Task<Result<RepositoryActionResult>> DeleteAsync(
    //    object id,
    //    ActiveEntityCallbackOptions<TEntity, TId> callbacks = null,
    //    CancellationToken cancellationToken = default);

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
    Task<Result<long>> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

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
    Task<Result<long>> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a single entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options (e.g., includes, no-tracking).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the found entity or null.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.FindOneAsync(customerId);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    Task<Result<TEntity>> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindOneAsync(spec);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    Task<Result<TEntity>> FindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindOneAsync(specs);
    /// if (result.IsSuccess && result.Value != null) { Console.WriteLine(result.Value.FirstName); }
    /// </code>
    /// </example>
    Task<Result<TEntity>> FindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all entities matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entities.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Take = 10 };
    /// var result = await provider.FindAllAsync(options);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TEntity>>> FindAllAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllAsync(spec);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TEntity>>> FindAllAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllAsync(specs);
    /// if (result.IsSuccess) { foreach (var customer in result.Value) { Console.WriteLine(customer.FirstName); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TEntity>>> FindAllAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all entities matching the given options with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering, includes).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entities and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await provider.FindAllPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}, Page: {result.Value.Page}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TEntity>> FindAllPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllPagedAsync(spec, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TEntity>> FindAllPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllPagedAsync(specs, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TEntity>> FindAllPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.ProjectAllAsync<Customer, string>(c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.ProjectAllAsync(spec, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.ProjectAllAsync(specs, c => c.FirstName);
    /// if (result.IsSuccess) { foreach (var name in result.Value) { Console.WriteLine(name); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.ProjectAllPagedAsync<Customer, string>(c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.ProjectAllPagedAsync(spec, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.ProjectAllPagedAsync(specs, c => c.FirstName, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities exist matching the given id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.ExistsAsync(1);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities exist matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.ExistsAsync();
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Entities exist"); }
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities exist matching the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var spec = new Specification<Customer>(c => c.LastName == "Doe");
    /// var result = await provider.ExistsAsync(spec);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities exist matching the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications to filter entities.</param>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing a boolean indicating if entities exist.</returns>
    /// <example>
    /// <code>
    /// var specs = new[] { new Specification<Customer>(c => c.LastName == "Doe") };
    /// var result = await provider.ExistsAsync(specs);
    /// if (result.IsSuccess && result.Value) { Console.WriteLine("Matching entities exist"); }
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities matching the given options.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the count of entities.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.CountAsync();
    /// if (result.IsSuccess) { Console.WriteLine($"Total entities: {result.Value}"); }
    /// </code>
    /// </example>
    Task<Result<long>> CountAsync(CancellationToken cancellationToken = default);

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
    /// var result = await provider.CountAsync(spec);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    Task<Result<long>> CountAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.CountAsync(specs);
    /// if (result.IsSuccess) { Console.WriteLine($"Matching entities: {result.Value}"); }
    /// </code>
    /// </example>
    Task<Result<long>> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all entity IDs matching the given options.
    /// </summary>
    /// <param name="options">Optional find options (e.g., filtering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the collection of entity IDs.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.FindAllIdsAsync();
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllIdsAsync(spec);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TId>>> FindAllIdsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllIdsAsync(specs);
    /// if (result.IsSuccess) { foreach (var id in result.Value) { Console.WriteLine(id); } }
    /// </code>
    /// </example>
    Task<Result<IEnumerable<TId>>> FindAllIdsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all entity IDs matching the given options with pagination.
    /// </summary>
    /// <param name="options">Optional find options (e.g., paging, ordering).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a ResultPaged containing the paged entity IDs and total count.</returns>
    /// <example>
    /// <code>
    /// var options = new FindOptions<Customer> { Skip = 10, Take = 5 };
    /// var result = await provider.FindAllIdsPagedAsync(options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TId>> FindAllIdsPagedAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllIdsPagedAsync(spec, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TId>> FindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

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
    /// var result = await provider.FindAllIdsPagedAsync(specs, options);
    /// if (result.IsSuccess) { Console.WriteLine($"Total IDs: {result.Value.TotalCount}"); }
    /// </code>
    /// </example>
    Task<ResultPaged<TId>> FindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction in the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result containing the transaction object.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.BeginTransactionAsync();
    /// if (result.IsSuccess) { var transaction = result.Value; /* Use transaction */ }
    /// </code>
    /// </example>
    Task<Result<IDatabaseTransaction>> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a transaction in the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var transactionResult = await provider.BeginTransactionAsync();
    /// if (transactionResult.IsSuccess) {
    ///     await provider.CommitAsync();
    /// }
    /// </code>
    /// </example>
    Task<Result> CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a transaction in the underlying storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task with a Result indicating success or failure.</returns>
    /// <example>
    /// <code>
    /// var transactionResult = await provider.BeginTransactionAsync();
    /// if (transactionResult.IsSuccess) {
    ///     await provider.RollbackAsync();
    /// }
    /// </code>
    /// </example>
    Task<Result> RollbackAsync(CancellationToken cancellationToken = default);
}
