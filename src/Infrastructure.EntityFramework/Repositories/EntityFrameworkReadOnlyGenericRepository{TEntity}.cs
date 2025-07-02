// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using BridgingIT.DevKit.Domain;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// EntityFrameworkReadOnlyRepositoryWrapper is a repository wrapper for read-only operations,
/// extending the functionality of EntityFrameworkReadOnlyGenericRepository to provide
/// better integration with Entity Framework Core. It ensures that the underlying data context
/// is used in a read-only mode for the specified entity type.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TContext">The type of the DbContext.</typeparam>
public class EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TContext>(ILoggerFactory loggerFactory, TContext context)
    : EntityFrameworkReadOnlyGenericRepository<TEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : DbContext
{ }

/// <summary>
/// Provides a read-only repository implementation for Entity Framework that supports
/// asynchronous operations for querying entities from the database.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public class EntityFrameworkReadOnlyGenericRepository<TEntity>
    : // TODO: rename to EntityFrameworkReadOnlykRepository + Obsolete
        IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Represents a read-only generic repository using Entity Framework.
    /// </summary>
    protected EntityFrameworkReadOnlyGenericRepository(EntityFrameworkRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    /// <summary>
    /// Provides a read-only generic repository implementation using Entity Framework.
    /// </summary>
    protected EntityFrameworkReadOnlyGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// A read-only generic repository implementation using Entity Framework.
    /// </summary>
    public EntityFrameworkReadOnlyGenericRepository(ILoggerFactory loggerFactory, DbContext context)
        : this(o => o.LoggerFactory(loggerFactory).DbContext(context)) { }

    /// <summary>
    /// Provides logging capabilities for the repository operations.
    /// </summary>
    /// <remarks>
    /// Utilizes the <see cref="ILogger"/> interface to log informational, warning, and error messages.
    /// The logger is typically configured during the initialization of the repository.
    /// </remarks>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Gets the options for the EntityFramework repository, which control various aspects such as logging,
    /// database context management, and other repository configurations.
    /// </summary>
    protected EntityFrameworkRepositoryOptions Options { get; }

    /// <summary>
    /// Retrieves all entities asynchronously from the repository that match the specified options.
    /// </summary>
    /// <param name="options">An optional parameter to specify filtering, sorting, and pagination options for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of entities matching the specified options.</returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// var entities = await repository.FindAllAsync();
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously retrieves all entities that match the specified criteria.
    /// </summary>
    /// <param name="specification">The specification defining the criteria entities must meet to be retrieved.</param>
    /// <param name="options">An optional set of options to customize the retrieval operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains an enumerable of entities matching the criteria.</returns>
    /// <example>
    /// var entities = await repository.FindAllAsync(specification, options, cancellationToken);
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously retrieves all entities that match the provided specifications and options.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="options">Optional parameters for finding entities such as sorting and paging.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable of entities that match the provided specifications and options.</returns>
    /// <example>
    /// <code>
    /// var specifications = new List<ISpecification<TEntity>> { specification1, specification2 };
    /// var options = new FindOptions<TEntity>();
    /// var result = await repository.FindAllAsync(specifications, options, CancellationToken.None);
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? [.. specifications];
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await this.Options.DbContext.Set<TEntity>()
                .AsNoTrackingIf(options) // , this.Options.Mapper
                .IncludeIf(options)
                .HierarchyIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                .DistinctIf(options)
                .SkipIf(options.Skip)
                .TakeIf(options.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext();
        }

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTrackingIf(options) // , this.Options.Mapper
            .IncludeIf(options)
            .HierarchyIf(options)
            .WhereExpressions(expressions)
            .DistinctIf(options)
            .SkipIf(options?.Skip)
            .TakeIf(options?.Take)
            .ToListAsyncSafe(cancellationToken)
            .AnyContext();
    }

    /// <summary>
    /// Projects all entities of type <typeparamref name="TEntity"/> using the specified <paramref name="projection"/> expression.
    /// </summary>
    /// <typeparam name="TProjection">The type of the projection result.</typeparam>
    /// <param name="projection">The expression used to project each entity.</param>
    /// <param name="options">Optional find options for the query.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable of the projected results.</returns>
    /// <example>
    /// <code>
    /// var projection = await repository.ProjectAllAsync(entity => new { entity.Id, entity.Name });
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([], projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities of type <typeparamref name="TEntity" /> that satisfy the provided specification
    /// to a collection of type <typeparamref name="TProjection" />.
    /// </summary>
    /// <typeparam name="TProjection">The type of the result projection.</typeparam>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="specification"></param>
    /// <param name="projection"></param>
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([specification], projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously projects all entities to the specified projection type, based on the provided specifications.
    /// </summary>
    /// <typeparam name="TProjection">The type to project the entities to.</typeparam>
    /// <param name="specifications">The collection of specifications used to filter the entities.</param>
    /// <param name="projection">The expression defining the projection from the entity to the projection type.</param>
    /// <param name="options">Optional find options to apply to the query, such as ordering, skipping, and taking.</param>
    /// <param name="cancellationToken">An optional cancellation token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the projected results as a collection of <typeparamref name="TProjection"/>.</returns>
    /// <example>
    /// <code>
    /// var projections = await repository.ProjectAllAsync(
    /// specifications,
    /// entity => new { entity.Id, entity.Name },
    /// options,
    /// cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? [.. specifications.SafeNull()];
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        if (options?.HasOrders() == true)
        {
            return await this.Options.DbContext.Set<TEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options)
                .HierarchyIf(options)
                .WhereExpressions(expressions)
                .OrderByIf(options)
                .Select(projection)
                .DistinctIf(options, this.Options.Mapper)
                .SkipIf(options.Skip)
                .TakeIf(options.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext();
        }

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTrackingIf(options, this.Options.Mapper)
            .IncludeIf(options)
            .HierarchyIf(options)
            .WhereExpressions(expressions)
            .Select(projection)
            .DistinctIf(options, this.Options.Mapper)
            .SkipIf(options?.Skip)
            .TakeIf(options?.Take)
            .ToListAsyncSafe(cancellationToken)
            .AnyContext();
    }

    /// <summary>
    /// Asynchronously finds an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to find.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity if found, otherwise null.</returns>
    /// <example>
    /// var repository = new EntityFrameworkGenericRepository();
    /// var user = await repository.FindOneAsync(userId);
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return null;
        }

        return await this.Options.DbContext.FindAsync(this.ConvertEntityId(id), options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously finds a single entity that matches the given specification and options.
    /// </summary>
    /// <param name="specification">The specification to match the entity against.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entity that matches the specification.</returns>
    /// <example>
    /// <code>
    /// var entity = await repository.FindOneAsync(specification, options, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously finds a single entity based on the provided specifications.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="options">Optional parameters to customize the find operation.</param>
    /// <param name="cancellationToken">Token to signal the asynchronous operation should be canceled.</param>
    /// <returns>A task representing the asynchronous operation, containing the single entity that matches the specifications, or null if no matching entity is found.</returns>
    /// <example>
    /// var specifications = new List<ISpecification<TEntity>> { spec1, spec2 };
    /// var entity = await repository.FindOneAsync(specifications);
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? [.. specifications];
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTrackingIf(options, this.Options.Mapper)
            .IncludeIf(options)
            .HierarchyIf(options)
            .WhereExpressions(expressions)
            .FirstOrDefaultAsync(cancellationToken)
            .AnyContext();
    }

    /// <summary>
    /// Checks if an entity with the specified ID exists asynchronously in the repository.
    /// </summary>
    /// <param name="id">The ID of the entity to check for existence.</param>
    /// <param name="cancellationToken">An optional token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the entity exists.</returns>
    /// <example>
    /// var exists = await repository.ExistsAsync(entityId);
    /// if (exists)
    /// {
    /// // Entity exists
    /// }
    /// </example>
    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        var result = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken)
            .AnyContext() is not null;

        return result;
    }

    /// <summary>
    /// Asynchronously counts the number of entities in the repository that match the given specifications.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of matching entities.</returns>
    /// <example>
    /// <code>
    /// var result = await repository.CountAsync(new List<ISpecification<TEntity>>());
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously counts the number of entities that satisfy the provided specification.
    /// </summary>
    /// <param name="specification">The specification that entities must satisfy to be counted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of entities that satisfy the provided specification.</returns>
    /// <example>
    /// var count = await repository.CountAsync(yourSpecification, cancellationToken);
    /// </example>
    public virtual async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously counts the number of entities that satisfy provided specifications.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of entities that satisfy the provided specifications.</returns>
    /// <example>
    /// <code>
    /// var count = await repository.CountAsync(new List<ISpecification<TEntity>> { spec1, spec2 }, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? [.. specifications];
        var expressions = specificationsArray.SafeNull().Select(s => s.ToExpression());

        return await this.Options.DbContext.Set<TEntity>()
            .AsNoTracking()
            .WhereExpressions(expressions)
            .LongCountAsync(cancellationToken)
            .AnyContext();
    }

    /// <summary>
    /// Retrieves the DbSet for the entity type TEntity from the DbContext.
    /// </summary>
    /// <returns>
    /// The DbSet of type TEntity from the configured DbContext.
    /// </returns>
    /// <example>
    /// var dbSet = GetDbSet();
    /// </example>
    protected DbSet<TEntity> GetDbSet()
    {
        return this.Options.DbContext.Set<TEntity>();
    }

    /// <summary>
    /// Retrieves the database connection from the current context.
    /// </summary>
    /// <returns>
    /// An <see cref="IDbConnection"/> instance representing the current database connection.
    /// </returns>
    /// <example>
    /// <code>
    /// var connection = repository.GetDbConnection();
    /// </code>
    /// </example>
    protected IDbConnection GetDbConnection()
    {
        return this.Options.DbContext.Database.GetDbConnection();
    }

    /// <summary>
    /// Retrieves the current database transaction from the configured DbContext.
    /// </summary>
    /// <returns>
    /// The current <see cref="IDbTransaction"/> if there is an active transaction;
    /// otherwise, null.
    /// </returns>
    /// <example>
    /// var transaction = repository.GetDbTransaction();
    /// </example>
    protected IDbTransaction GetDbTransaction()
    {
        return this.Options.DbContext.Database.CurrentTransaction?.GetDbTransaction();
    }

    /// <summary>
    /// Converts the provided entity ID value to the appropriate type for the entity.
    /// </summary>
    /// <param name="value">The entity ID value to convert.</param>
    /// <returns>The converted entity ID value.</returns>
    /// <example>
    /// var convertedId = ConvertEntityId("1234-5678-90"); // Converts to Guid, int, or long based on the type of TEntity.
    /// </example>
    private object ConvertEntityId(object value)
    {
        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(Guid) &&
            value?.GetType() == typeof(string))
        {
            // string to guid conversion
            return Guid.Parse(value.ToString());
        }

        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(int) &&
            value?.GetType() == typeof(string))
        {
            // int to guid conversion
            return int.Parse(value.ToString());
        }

        if (typeof(TEntity).GetPropertyUnambiguous("Id")?.PropertyType == typeof(long) &&
            value?.GetType() == typeof(string))
        {
            // long to guid conversion
            return long.Parse(value.ToString());
        }

        return value;
    }
}