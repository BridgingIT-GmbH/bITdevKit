// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// Provides a repository wrapper implementation using Entity Framework for CRUD operations on entities.
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity.</typeparam>
/// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
/// <typeparam name="TContext">The type of the database context.</typeparam>
public class EntityFrameworkRepositoryWrapper<TEntity, TDatabaseEntity, TContext>(
    ILoggerFactory loggerFactory,
    TContext context,
    IEntityMapper mapper) : EntityFrameworkGenericRepository<TEntity, TDatabaseEntity>(loggerFactory, context, mapper)
    where TEntity : class, IEntity
    where TDatabaseEntity : class
    where TContext : DbContext
{ }

/// <summary>
/// Provides a generic repository implementation using Entity Framework.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDatabaseEntity">The type of the database entity.</typeparam>
public class
    EntityFrameworkGenericRepository<TEntity, TDatabaseEntity> // TODO: rename to EntityFrameworkRepository + Obsolete
    : EntityFrameworkReadOnlyGenericRepository<TEntity, TDatabaseEntity>, IGenericRepository<TEntity>
    where TEntity : class, IEntity
    where TDatabaseEntity : class
{
    /// <summary>
    /// Provides generic repository functionalities for entities in an Entity Framework context.
    /// </summary>
    public EntityFrameworkGenericRepository(EntityFrameworkRepositoryOptions options)
        : base(options) { }

    /// <summary>
    /// Provides a generic repository for entities using Entity Framework.
    /// </summary>
    protected EntityFrameworkGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// Represents a generic repository implementation using Entity Framework for read/write operations.
    /// </summary>
    protected EntityFrameworkGenericRepository(ILoggerFactory loggerFactory, DbContext context, IEntityMapper mapper)
        : base(o => o.LoggerFactory(loggerFactory).DbContext(context).Mapper(mapper)) { }

    /// <summary>
    /// Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous insert operation. The task result contains the inserted entity.</returns>
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    /// Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The updated entity.</returns>
    /// <example>
    /// Example usage:
    /// <code>
    /// var updatedEntity = await repository.UpdateAsync(entity, cancellationToken);
    /// </code>
    /// </example>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    /// Inserts or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation, with a tuple containing the updated or inserted entity and the action performed.</returns>
    /// <example>
    /// var result = await repository.UpsertAsync(myEntity);
    /// </example>
    public virtual async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return (null, RepositoryActionResult.None);
        }

        TDatabaseEntity existingEntity = null;
        var isNew = IsDefaultId(entity.Id);
        if (!isNew) // Check if the entity already exists in the database
        {
            existingEntity = await this.Options.DbContext.Set<TDatabaseEntity>().FindAsync([this.ConvertEntityId(entity.Id)], cancellationToken).AnyContext();
            isNew = existingEntity == null;
        }

        if (isNew)
        {
            return await HandleInsert(entity, cancellationToken);
        }

        return await HandleUpdate(entity, existingEntity, cancellationToken);

        async Task<(TEntity entity, RepositoryActionResult action)> HandleInsert(TEntity entity, CancellationToken cancellationToken)
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - insert (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);

            if (entity is IConcurrency concurrencyEntity)
            {
                concurrencyEntity.ConcurrencyVersion = this.Options.VersionGenerator();
            }

            var dbEntity = this.Options.Mapper.Map<TDatabaseEntity>(entity);
            this.Options.DbContext.Set<TDatabaseEntity>().Add(dbEntity);

            if (this.Options.Autosave)
            {
                await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
                this.Options.Mapper.Map(dbEntity, entity);
            }

            return (entity, RepositoryActionResult.Inserted);
        }

        async Task<(TEntity entity, RepositoryActionResult action)> HandleUpdate(TEntity entity, TDatabaseEntity existingEntity, CancellationToken cancellationToken)
        {
            this.Logger.LogDebug("{LogKey} repository: upsert - update (type={entityType}, id={entityId})", Constants.LogKey, typeof(TEntity).Name, entity.Id);

            if (entity is IConcurrency concurrencyEntity && existingEntity is IConcurrency existingConcurrencyEntity && this.Options.EnableOptimisticConcurrency)
            {
                var originalVersion = concurrencyEntity.ConcurrencyVersion;
                concurrencyEntity.ConcurrencyVersion = this.Options.VersionGenerator();

                this.Options.DbContext.Entry(existingEntity) // Set the original version for concurrency check
                    .Property(nameof(IConcurrency.ConcurrencyVersion)).OriginalValue = originalVersion;
            }

            // Update values including the new version
            this.Options.DbContext.Entry(existingEntity).CurrentValues
                .SetValues(this.Options.Mapper.Map<TDatabaseEntity>(entity));

            if (this.Options.Autosave)
            {
                foreach (var entry in this.Options.DbContext.ChangeTracker.Entries())
                {
                    this.Logger.LogTrace("{LogKey} dbcontext entity state: {entityType} (keySet={entityKeySet}) -> {entryState}", Constants.LogKey, entry.Entity.GetType().Name, entry.IsKeySet, entry.State);
                }

                await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
            }

            return (entity, RepositoryActionResult.Updated);
        }
    }

    /// <summary>
    /// Deletes the entity with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A result indicating the outcome of the delete operation.</returns>
    /// <example>await repository.DeleteAsync(entityId, cancellationToken);</example>
    public virtual async Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.NotFound;
        }

        var existingEntity = await this.Options.DbContext.Set<TDatabaseEntity>().FindAsync([this.ConvertEntityId(id) /*, cancellationToken: cancellationToken*/], cancellationToken).AnyContext(); // INFO: don't use this.FindOne here, existingEntity should be a TDatabaseEntity for the Remove to work
        if (existingEntity is not null)
        {
            this.Options.DbContext.Remove(existingEntity);

            if (this.Options.Autosave)
            {
                await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
            }

            return RepositoryActionResult.Deleted;
        }

        return RepositoryActionResult.NotFound;
    }

    /// <summary>
    /// Deletes the provided entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">Optional. The CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous delete operation. The task result contains the action result status.</returns>
    /// <example>
    /// var repository = new EntityFrameworkGenericRepository<SomeEntity, SomeDatabaseEntity>(options);
    /// var result = await repository.DeleteAsync(entityToDelete, cancellationToken);
    /// </example>
    public virtual async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity?.Id is null)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(entity.Id, cancellationToken).AnyContext();
    }

    // Helper method to check if Id is at its default value
    private static bool IsDefaultId(object id)
    {
        if (id == null)
        {
            return true; // null is considered default for reference types like string
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
            // Add other types as needed (e.g., short, byte, custom structs)
            _ => Equals(id, Activator.CreateInstance(idType)) // Fallback for value types
        };
    }
}