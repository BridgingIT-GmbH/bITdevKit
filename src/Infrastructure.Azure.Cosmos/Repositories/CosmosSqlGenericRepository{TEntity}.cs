// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;
using Common;
using Domain.Model;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// CosmosSqlRepositoryWrapper class is a database repository wrapper that inherits from
/// CosmosSqlGenericRepository for operations with Cosmos DB using a given provider.
/// </summary>
/// <typeparam name="TEntity">The type of entity.</typeparam>
/// <typeparam name="TProvider">The type of the Cosmos SQL provider.</typeparam>
public class CosmosSqlRepositoryWrapper<TEntity, TProvider>(ILoggerFactory loggerFactory, TProvider provider)
    : CosmosSqlGenericRepository<TEntity>(loggerFactory, provider)
    where TEntity : class, IEntity
    where TProvider : ICosmosSqlProvider<TEntity> { }

/// <summary>
/// A generic repository for managing entities within Azure Cosmos DB using SQL API.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class CosmosSqlGenericRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// A generic repository for interacting with data stored in Cosmos DB SQL API.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity this repository manages.</typeparam>
    /// <example>
    /// var myRepository = new CosmosSqlGenericRepository<MyEntity>(options);
    /// </example>
    public CosmosSqlGenericRepository(CosmosSqlGenericRepositoryOptions<TEntity> options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Provider, nameof(options.Provider));

        this.Options = options;
        this.Options.IdGenerator ??= new EntityGuidIdGenerator<TEntity>();
        this.Logger = options.LoggerFactory?.CreateLogger<CosmosSqlGenericRepository<TEntity>>() ?? NullLoggerFactory.Instance.CreateLogger<CosmosSqlGenericRepository<TEntity>>();
        this.Provider = options.Provider;
    }

    /// <summary>
    /// A generic repository implementation for Cosmos SQL. </summary> <typeparam name="TEntity">The type of the entity.</typeparam> <example>
    /// var repository = new CosmosSqlGenericRepository<MyEntity>(options); </example>
    /// /
    public CosmosSqlGenericRepository(
        Builder<CosmosSqlGenericRepositoryOptionsBuilder<TEntity>, CosmosSqlGenericRepositoryOptions<TEntity>>
            optionsBuilder)
        : this(optionsBuilder(new CosmosSqlGenericRepositoryOptionsBuilder<TEntity>()).Build()) { }

    /// <summary>
    /// Represents a generic repository for handling entities in an Azure Cosmos SQL database.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    public CosmosSqlGenericRepository(ILoggerFactory loggerFactory, ICosmosSqlProvider<TEntity> provider)
        : this(o => o.LoggerFactory(loggerFactory).Provider(provider)) { }

    /// <summary>
    /// Gets the options for configuring the Cosmos SQL Generic Repository.
    /// The options include settings such as the provider and ID generation strategy.
    /// </summary>
    protected CosmosSqlGenericRepositoryOptions<TEntity> Options { get; }

    /// <summary>
    /// Gets an instance of the logger used for logging repository operations and diagnostics.
    /// </summary>
    /// <example>
    /// Usage:
    /// <code>
    /// private readonly ILogger<CosmosSqlGenericRepository<MyEntity>> _logger;
    /// </code>
    /// </example>
    protected ILogger<CosmosSqlGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Provides access to an underlying data provider for executing
    /// data operations such as creating, reading, updating, and deleting entities.
    /// </summary>
    /// <example>
    /// Sample usage:
    /// <code>
    /// var provider = new CosmosSqlProvider<TEntity>(...);
    /// var repository = new CosmosSqlGenericRepository<TEntity>(loggerFactory, provider);
    /// var entities = await repository.FindAllAsync();
    /// </code>
    /// </example>
    protected ICosmosSqlProvider<TEntity> Provider { get; }

    /// <summary>
    /// Retrieves all entities that satisfy the given options.
    /// </summary>
    /// <param name="options">The options for finding entities.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An enumerable collection of entities.</returns>
    /// <example>
    /// <code>
    /// var result = await repository.FindAllAsync(findOptions, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Retrieves all entities that match the specified criteria.
    /// </summary>
    /// <param name="specification">The specification criteria to filter entities.</param>
    /// <param name="options">Optional parameters for fine-tuning the query.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of entities.</returns>
    /// <example>
    /// var items = await repository.FindAllAsync(specification, options, cancellationToken);
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously retrieves all entities that match the given specifications.
    /// </summary>
    /// <param name="specifications">A collection of specifications used to filter the entities.</param>
    /// <param name="options">Options for retrieving entities, such as ordering and pagination. Optional.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation. Optional.</param>
    /// <returns>A task representing the asynchronous operation, with a result of an enumerable collection of entities.</returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// var entities = await repository.FindAllAsync(specifications, options, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression().Expand()); // expand fixes Invoke in expression issue
        var order = (options?.Orders ?? new List<OrderOption<TEntity>>()).Insert(options?.Order).FirstOrDefault(); // cosmos only supports single orderby

        if (options?.Distinct is not null)
        {
            throw new NotSupportedException("Distinct is not supported for Cosmos");
        }

        return (await this.Provider.ReadItemsAsync(expressions,
                options?.Skip ?? -1,
                options?.Take ?? -1,
                order?.Expression,
                order?.Direction == OrderDirection.Descending,
                cancellationToken: cancellationToken).AnyContext()).ToList();
    }

    /// <summary>
    /// Projects all entities using the specified projection expression.
    /// </summary>
    /// <param name="projection">The projection expression to use for selecting data.</param>
    /// <param name="options">Optional find options for filtering or sorting the data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <typeparam name="TProjection">The type of the projection result.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of projected entities.</returns>
    /// <example>
    /// var projections = await repository.ProjectAllAsync(entity => new { entity.Id, entity.Name });
    /// </example>
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Projects all entities that match the given specification.
    /// </summary>
    /// <typeparam name="TProjection">The type of the projected result.</typeparam>
    /// <param name="specification">The specification used to filter the entities.</param>
    /// <param name="projection">The projection expression to shape the result.</param>
    /// <param name="options">Optional settings for finding entities.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of projected entities.</returns>
    /// <example>
    /// var result = await repository.ProjectAllAsync(
    /// spec,
    /// entity => new { entity.Id, entity.Name },
    /// findOptions,
    /// CancellationToken.None);
    /// </example>
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Projects all entities that match the provided specifications to the specified projection type.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="projection">The projection expression to select specific fields from the entities.</param>
    /// <param name="options">Optional find options to customize the query execution.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <typeparam name="TProjection">The type to which the results are projected.</typeparam>
    /// <returns>An asynchronous task that returns an enumerable of projected entities.</returns>
    /// <example>
    /// var projections = await repository.ProjectAllAsync(specifications, projection, options, cancellationToken);
    /// </example>
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Finds and returns an entity identified by the given id.
    /// </summary>
    /// <param name="id">The identifier of the entity to find.</param>
    /// <param name="options">Optional find options to apply during the retrieval process.</param>
    /// <param name="cancellationToken">An optional cancellation token to cancel the operation.</param>
    /// <returns>The entity identified by the given id, or null if not found.</returns>
    /// <example>
    /// var entity = await repository.FindOneAsync(id, options);
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return default;
        }

        return await this.Provider.ReadItemAsync(id.ToString(), cancellationToken: cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity based on the provided specification and options.
    /// </summary>
    /// <param name="specification">The specification criteria to find the entity.</param>
    /// <param name="options">Optional parameters to control the query.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity that matches the given specification, or null if no match is found.</returns>
    /// <example>
    /// var entity = await repository.FindOneAsync(specification, options, cancellationToken);
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds a single entity that satisfies the given specifications.
    /// </summary>
    /// <param name="specifications">The specifications that the entity should satisfy.</param>
    /// <param name="options">Optional find options to customize the search.</param>
    /// <param name="cancellationToken">Token to cancel the search operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the found entity or null if not found.</returns>
    /// <example>
    /// var entity = await repository.FindOneAsync(specifications, options, cancellationToken);
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression().Expand()); // expand fixes Invoke in expression issue

        return (await this.Options.Provider.ReadItemsAsync(expressions, -1, 1, cancellationToken: cancellationToken).AnyContext()).FirstOrDefault();
    }

    /// <summary>
    /// Checks if an entity with the specified ID exists in the repository.
    /// </summary>
    /// <param name="id">The ID of the entity to check for existence.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the entity exists; otherwise, <c>false</c>.</returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// bool exists = await repository.ExistsAsync(entityId);
    /// </code>
    /// </example>
    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        return await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext() is not null;
    }

    /// <summary>
    /// Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the inserted entity.</returns>
    /// <example>
    /// var insertedEntity = await repository.InsertAsync(entity);
    /// </example>
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return (await this.UpsertAsync(entity, cancellationToken).AnyContext()).entity;
    }

    /// <summary>
    /// Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>The updated entity.</return>
    /// <example>
    /// <code>
    /// var updatedEntity = await repository.UpdateAsync(entity, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return (await this.UpsertAsync(entity, cancellationToken).AnyContext()).entity;
    }

    /// <summary>
    /// Inserts or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing the entity and a result indicating whether the entity was inserted or updated.
    /// </returns>
    /// <example>
    /// var result = await repository.UpsertAsync(entity);
    /// if (result.action == RepositoryActionResult.Inserted)
    /// {
    /// Console.WriteLine("Entity was inserted.");
    /// }
    /// else if (result.action == RepositoryActionResult.Updated)
    /// {
    /// Console.WriteLine("Entity was updated.");
    /// }
    /// </example>
    public virtual async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return (default, RepositoryActionResult.None);
        }

        var isNew = this.Options.IdGenerator.IsNew(entity.Id) || !await this.ExistsAsync(entity.Id, cancellationToken).AnyContext();
        if (isNew)
        {
            this.Options.IdGenerator.SetNew(entity); // cosmos v3 needs an id, also for new items
        }

        var result = await this.Provider.UpsertItemAsync(entity, cancellationToken: cancellationToken).AnyContext();

        return isNew ? (result, RepositoryActionResult.Inserted) : (result, RepositoryActionResult.Updated);
    }

    /// <summary>
    /// Deletes the entity with the specified id.
    /// </summary>
    /// <param name="id">The id of the entity to delete.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous delete operation. The task result contains the action result of the delete operation.</returns>
    /// <example>
    /// <code>
    /// var result = await repository.DeleteAsync(entityId, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var entity = await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();
        if (entity is not null)
        {
            return await this.DeleteAsync(entity, cancellationToken).AnyContext();
        }

        return RepositoryActionResult.None;
    }

    /// <summary>
    /// Deletes the provided entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">An optional token to cancel the asynchronous operation.</param>
    /// <return>Returns a <see cref="RepositoryActionResult"/> indicating the result of the delete operation.</return>
    /// <example>
    /// var result = await repository.DeleteAsync(entity, cancellationToken);
    /// </example>
    public virtual async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity?.Id == default)
        {
            return RepositoryActionResult.None;
        }

        var response = await this.Provider.DeleteItemAsync(entity.Id.ToString(), cancellationToken: cancellationToken).AnyContext();
        return response ? RepositoryActionResult.Deleted : RepositoryActionResult.None;
    }

    /// <summary>
    /// Asynchronously counts the number of entities that meet the provided specification.
    /// </summary>
    /// <param name="specification">The specification that entities must meet to be counted.</param>
    /// <param name="cancellationToken">Optional. The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of entities that meet the specification.</returns>
    /// <example>
    /// <code>
    /// var count = await repository.CountAsync(specification, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously counts all entities in the repository.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The total count of entities in the repository.</returns>
    /// <example>
    /// This example shows how to call the CountAsync method.
    /// <code>
    /// long count = await repository.CountAsync(CancellationToken.None);
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Returns the count of entities that match the provided specifications asynchronously.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="cancellationToken">A cancellation token to observe while awaiting the task.</param>
    /// <returns>The count of entities that match the provided specifications.</returns>
    /// <example>
    /// <code>
    /// var count = await repository.CountAsync(new[] { specification }, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression().Expand()); // expand fixes Invoke in expression issue

        return (await this.Provider.ReadItemsAsync(expressions, cancellationToken: cancellationToken).AnyContext()).LongCount();
    }
}