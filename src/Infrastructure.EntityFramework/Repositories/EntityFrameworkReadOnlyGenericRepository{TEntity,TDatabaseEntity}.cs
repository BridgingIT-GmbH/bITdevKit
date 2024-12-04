// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Data;
using BridgingIT.DevKit.Domain;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// A read-only repository wrapper implementation for Entity Framework, providing
/// data access functionalities using <typeparamref name="TDatabaseEntity"/> and
/// managing <typeparamref name="TEntity"/> entities in a <typeparamref name="TContext"/> context.
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity.</typeparam>
/// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
/// <typeparam name="TContext">The type of the Entity Framework database context.</typeparam>
public class EntityFrameworkReadOnlyRepositoryWrapper<TEntity, TDatabaseEntity, TContext>(
    ILoggerFactory loggerFactory,
    TContext context) : EntityFrameworkReadOnlyGenericRepository<TEntity, TDatabaseEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : DbContext
    where TDatabaseEntity : class { }

/// <summary>
/// Provides a read-only generic repository for Entity Framework.
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity.</typeparam>
/// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
public class EntityFrameworkReadOnlyGenericRepository<TEntity, TDatabaseEntity>
    : // TODO: rename to EntityFrameworkReadOnlykRepository + Obsolete
        IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    /// <summary>
    /// Provides a generic read-only repository implementation based on Entity Framework Core.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <typeparam name="TDatabaseEntity">The type of database entity.</typeparam>
    protected EntityFrameworkReadOnlyGenericRepository(EntityFrameworkRepositoryOptions options)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.DbContext, nameof(options.DbContext));
        EnsureArg.IsNotNull(options.Mapper, nameof(options.Mapper));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    /// <summary>
    /// A generic read-only repository implementation using Entity Framework.
    /// </summary>
    /// <typeparam name="TEntity">The entity type in the domain model.</typeparam>
    /// <typeparam name="TDatabaseEntity">The entity type in the database model.</typeparam>
    /// <remarks>
    /// This class provides basic read-only operations. The repository must be
    /// configured with Entity Framework options, such as DbContext and Logger.
    /// </remarks>
    /// <example>
    /// <code>
    /// var repository = new EntityFrameworkReadOnlyGenericRepository<MyEntity, MyDbEntity>(loggerFactory, dbContext, mapper);
    /// </code>
    /// </example>
    protected EntityFrameworkReadOnlyGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// A read-only generic repository for use with Entity Framework.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
    protected EntityFrameworkReadOnlyGenericRepository(
        ILoggerFactory loggerFactory,
        DbContext context,
        IEntityMapper mapper = null)
        : this(o => o.LoggerFactory(loggerFactory).DbContext(context).Mapper(mapper)) { }

    /// <summary>
    /// Provides logging capabilities for instances of <see cref="IGenericRepository{TEntity}"/>.
    /// This property uses a configured <see cref="ILogger{T}"/> to log various repository operations,
    /// such as insert, update, and detailed state transitions during the Entity Framework operations.
    /// </summary>
    /// <example>
    /// Usage within a repository method:
    /// <code>
    /// this.Logger.LogDebug("{LogKey} repository: upsert - insert (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);
    /// </code>
    /// </example>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Gets the configuration options for the Entity Framework repository.
    /// </summary>
    /// <example>
    /// The following example demonstrates how you might access the <c>Options</c> property:
    /// <code>
    /// var options = repository.Options;
    /// </code>
    /// </example>
    protected EntityFrameworkRepositoryOptions Options { get; }

    /// <summary>
    /// Asynchronously retrieves all entities of type TEntity based on the provided options.
    /// </summary>
    /// <param name="options">An optional parameter specifying the options to use when retrieving the entities.</param>
    /// <param name="cancellationToken">An optional parameter that allows the operation to be canceled.</param>
    /// <returns>An enumerable collection of TEntity.</returns>
    /// <example>
    /// var options = new FindOptions<TEntity>();
    /// var entities = await repository.FindAllAsync(options, cancellationToken);
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously finds all entities that match the specified criteria.
    /// </summary>
    /// <param name="specification">The specification to filter the entities.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous task that, when completed, contains the collection of entities that match the specified criteria.</returns>
    /// <example>
    /// <code>
    /// var entities = await repository.FindAllAsync(specification);
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously retrieves all entities matching the given specifications and options.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="options">Optional find options to apply to the query.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of entities that match the given specifications and options.</returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// var entities = await repository.FindAllAsync(new List<ISpecification<TEntity>> { specification }, options, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s));

        if (options?.HasOrders() == true)
        {
            return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .OrderByIf(options, this.Options.Mapper)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext()).Select(d => this.Options.Mapper.Map<TEntity>(d));
        }

        return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext())
            .Select(d =>
                this.Options.Mapper
                    .Map<TEntity>(d)); // mapping needs to be done client-side, otherwise ef core sql translation error
    }

    /// <summary>
    /// Projects all entities as per the specified projection expression asynchronously.
    /// </summary>
    /// <typeparam name="TProjection">The type of the projection.</typeparam>
    /// <param name="projection">The projection expression to apply on the entities.</param>
    /// <param name="options">Optional find options to customize the query.</param>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task representing the asynchronous operation, containing a collection of projected entities.
    /// </returns>
    /// <example>
    /// <code>
    /// var projections = await repository.ProjectAllAsync(
    /// entity => new { entity.Property1, entity.Property2 },
    /// null,
    /// CancellationToken.None);
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
    /// Projects all entities that satisfy the given specification to a specified projection type asynchronously.
    /// </summary>
    /// <typeparam name="TProjection">The type of projection.</typeparam>
    /// <param name="specification">The specification used to filter the entities.</param>
    /// <param name="projection">The expression defining the projection.</param>
    /// <param name="options">The options for finding the entities, if any.</param>
    /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
    /// <returns>An enumerable of the projected entities.</returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// var result = await repository.ProjectAllAsync(specification, entity => new { entity.Property });
    /// </code>
    /// </example>
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([specification], projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Projects all entities of type <typeparamref name="TEntity"/> to a different type <typeparamref name="TProjection"/> based on the specified projection expression.
    /// </summary>
    /// <typeparam name="TProjection">The type to project the entities to.</typeparam>
    /// <param name="specifications">A collection of specifications to filter the entities.</param>
    /// <param name="projection">A projection expression to transform each <typeparamref name="TEntity"/> into <typeparamref name="TProjection"/>.</param>
    /// <param name="options">Options for finding the entities, can be null.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>An asynchronous task that returns an enumerable of the projected entities when completed.</returns>
    /// <example>
    /// var projectedEntities = await repository.ProjectAllAsync(specifications, projection, options, cancellationToken);
    /// </example>
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.SafeNull().ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s));

        if (options?.HasOrders() == true)
        {
            return (await this.Options.DbContext.Set<TDatabaseEntity>()
                    .AsNoTrackingIf(options, this.Options.Mapper)
                    .IncludeIf(options, this.Options.Mapper)
                    .WhereExpressions(expressions)
                    .OrderByIf(options, this.Options.Mapper)
                    .DistinctByIf(options, this.Options.Mapper)
                    .SkipIf(options?.Skip)
                    .TakeIf(options?.Take)
                    .ToListAsyncSafe(cancellationToken)
                    .AnyContext())
                .Select(d => this.Options.Mapper.Map<TEntity>(d)) // mapping needs to be done client-side, otherwise ef core sql translation error
                .Select(e => projection.Compile().Invoke(e)); // thus the projection can also be only done client-side
        }

        return (await this.Options.DbContext.Set<TDatabaseEntity>()
                .AsNoTrackingIf(options, this.Options.Mapper)
                .IncludeIf(options, this.Options.Mapper)
                .WhereExpressions(expressions)
                .DistinctByIf(options, this.Options.Mapper)
                .SkipIf(options?.Skip)
                .TakeIf(options?.Take)
                .ToListAsyncSafe(cancellationToken)
                .AnyContext())
            .Select(d => this.Options.Mapper.Map<TEntity>(d)) // mapping needs to be done client-side, otherwise ef core sql translation error
            .Select(e => projection.Compile().Invoke(e)); // thus the projection can also be only done client-side
    }

    /// <summary>
    /// Asynchronously counts the number of entities in the repository that match the given specifications.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities to be counted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of entities that match the given specifications.</returns>
    /// <example>
    /// <code>
    /// var count = await repository.CountAsync(specifications, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Counts the number of entities that satisfy the given specification.
    /// </summary>
    /// <param name="specification">The specification to filter the entities.</param>
    /// <param name="cancellationToken">A cancellation token to observe during the task execution.</param>
    /// <returns>A Task representing the asynchronous operation, with a long result representing the count of entities.</returns>
    /// <example>
    /// The following example demonstrates how to use the CountAsync method:
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
    /// Asynchronously counts the total number of database entities that match the provided specifications.
    /// </summary>
    /// <param name="specifications">A collection of specifications to filter the entities to be counted.</param>
    /// <param name="cancellationToken">An optional token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of entities that match the specifications.</returns>
    /// <example>
    /// This example shows how to use the CountAsync method to count entities:
    /// <code>
    /// var count = await repository.CountAsync(new List<ISpecification<TEntity>> { spec1, spec2 }, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s));

        return await this.Options.DbContext.Set<TDatabaseEntity>()
            .AsNoTracking()
            .WhereExpressions(expressions)
            .LongCountAsync(cancellationToken).AnyContext();
    }

    /// <summary>
    /// Asynchronously finds a single entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to find.</param>
    /// <param name="options">Optional parameters to customize the find operation.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the found entity, or <c>null</c> if no entity was found.</returns>
    /// <example>
    /// <code>
    /// var entity = await repository.FindOneAsync(entityId);
    /// </code>
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

        return this.Options.Mapper.Map<TEntity>(await this.Options.DbContext.FindAsync<TEntity, TDatabaseEntity>(
                this.ConvertEntityId(id),
                options,
                this.Options.Mapper,
                cancellationToken).AnyContext());
    }

    /// <summary>
    /// Asynchronously finds and returns a single entity that matches the given specification.
    /// </summary>
    /// <param name="specification">The specification to match the entity against.</param>
    /// <param name="options">Optional find options, such as sorting or paging.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the found entity, or null if no entity matches the specification.</returns>
    /// <example>
    /// The following example shows how to use the FindOneAsync method to retrieve an entity.
    /// <code>
    /// var specification = new SomeSpecification();
    /// var options = new SomeFindOptions();
    /// var entity = await repository.FindOneAsync(specification, options);
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
    /// Asynchronously finds one entity that matches the provided specifications.
    /// </summary>
    /// <typeparam name="TEntity">The type of the domain entity.</typeparam>
    /// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
    /// <param name="specifications">The list of specifications the entity must satisfy.</param>
    /// <param name="options">Optional find options to include or track entities during the search.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is the entity that matches the specifications or null if no entity is found.</returns>
    /// <example>
    /// To find an entity that matches certain criteria:
    /// <code>
    /// var entity = await repository.FindOneAsync(new List<ISpecification<MyEntity>> { spec1, spec2 }, options, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var specificationsArray = specifications as ISpecification<TEntity>[] ?? specifications.ToArray();
        var expressions = specificationsArray.SafeNull()
            .Select(s => this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(s)).ToList();

        return this.Options.Mapper.Map<TEntity>(await this.Options.DbContext.Set<TDatabaseEntity>()
            .AsNoTrackingIf(options, this.Options.Mapper)
            .IncludeIf(options, this.Options.Mapper)
            .WhereExpressions(expressions)
            .FirstOrDefaultAsync(cancellationToken).AnyContext());
    }

    /// <summary>
    /// Asynchronously checks if an entity with the given identifier exists in the repository.
    /// </summary>
    /// <param name="id">The identifier of the entity to check for existence.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the entity exists.</returns>
    /// <example>
    /// <code>
    /// var repository = new EntityFrameworkReadOnlyGenericRepository<MyEntity, MyDbEntity>();
    /// bool exists = await repository.ExistsAsync(12345);
    /// </code>
    /// </example>
    public virtual async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        return await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext() is not null;
    }

    /// <summary>
    /// Retrieves the <see cref="DbSet{TDatabaseEntity}"/> for the specified <typeparamref name="TDatabaseEntity"/> type.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="DbSet{TDatabaseEntity}"/> for accessing entities of type <typeparamref name="TDatabaseEntity"/> in the database context.
    /// </returns>
    /// <example>
    /// var dbSet = repository.GetDbSet();
    /// </example>
    protected DbSet<TDatabaseEntity> GetDbSet()
    {
        return this.Options.DbContext.Set<TDatabaseEntity>();
    }

    /// <summary>
    /// Retrieves the database connection associated with the current repository context.
    /// </summary>
    /// <returns>
    /// The database connection of type <see cref="IDbConnection"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// using var connection = repository.GetDbConnection();
    /// </code>
    /// </example>
    protected IDbConnection GetDbConnection()
    {
        return this.Options.DbContext.Database.GetDbConnection();
    }

    /// <summary>
    /// Retrieves the current database transaction from the DbContext.
    /// </summary>
    /// <returns>
    /// The current <see cref="IDbTransaction"/> object if there is an active transaction; otherwise, null.
    /// </returns>
    /// <example>
    /// Usage:
    /// <code>
    /// var transaction = repository.GetDbTransaction();
    /// </code>
    /// </example>
    protected IDbTransaction GetDbTransaction()
    {
        return this.Options.DbContext.Database.CurrentTransaction?.GetDbTransaction();
    }

    /// <summary>
    /// Converts the given entity ID to a type that matches the ID property type of the entity.
    /// </summary>
    /// <param name="value">The ID value to be converted.</param>
    /// <returns>The converted ID value if a conversion is necessary; otherwise, the original ID value.</returns>
    /// <example>
    /// var convertedId = repository.ConvertEntityId("some-string-id");
    /// </example>
    protected object ConvertEntityId(object value)
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