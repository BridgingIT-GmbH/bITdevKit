// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// EntityFrameworkRepositoryWrapper is a repository implementation using Entity Framework Core.
/// It provides a wrapper around the generic repository to handle CRUD operations for a specific entity type.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TContext">The type of the DbContext.</typeparam>
public class EntityFrameworkRepositoryWrapper<TEntity, TContext>(ILoggerFactory loggerFactory, TContext context)
    : EntityFrameworkGenericRepository<TEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : DbContext
{ }

/// <summary>
/// EntityFrameworkGenericRepository is a generic repository for Entity Framework,
/// providing essential CRUD operations for entity types.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public partial class EntityFrameworkGenericRepository<TEntity>
    : // TODO: rename to EntityFrameworkRepository + Obsolete
        EntityFrameworkReadOnlyGenericRepository<TEntity>, IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// A generic repository class for Entity Framework, providing basic CRUD operations on entities.
    /// </summary>
    protected EntityFrameworkGenericRepository(EntityFrameworkRepositoryOptions options)
        : base(options) { }

    /// <summary>
    /// Specifies an Entity Framework repository to manage entities of type <typeparamref name="TEntity"/>.
    /// </summary>
    public EntityFrameworkGenericRepository(
        Builder<EntityFrameworkRepositoryOptionsBuilder, EntityFrameworkRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new EntityFrameworkRepositoryOptionsBuilder()).Build()) { }

    /// <summary>
    /// A generic repository for performing CRUD operations on an entity using Entity Framework.
    /// </summary>
    public EntityFrameworkGenericRepository(ILoggerFactory loggerFactory, DbContext context)
        : base(o => o.LoggerFactory(loggerFactory).DbContext(context)) { }

    /// <summary>
    /// Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous insert operation. The task result contains the inserted entity.</returns>
    /// <example>
    /// <code>
    /// var entity = new YourEntity();
    /// var repository = new EntityFrameworkGenericRepository(context);
    /// var insertedEntity = await repository.InsertAsync(entity);
    /// </code>
    /// </example>
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    /// Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated entity.</returns>
    /// <example>
    /// var updatedEntity = await repository.UpdateAsync(entity);
    /// </example>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    /// <summary>
    /// Insert or updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// A tuple containing the entity and the result of the action (Inserted or Updated).
    /// </returns>
    public virtual async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return (null, RepositoryActionResult.None);
        }

        var isNew = IsDefaultId(entity.Id);
        if (!isNew) // Check if the entity already exists in the database
        {
            var existingEntity = await this.FindOneAsync(entity.Id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext();
            isNew = existingEntity == null;
        }

        if (isNew)
        {
            HandleInsert();
        }
        else
        {
            HandleUpdate();
        }

        if (this.Options.Autosave)
        {
            foreach (var entry in this.Options.DbContext.ChangeTracker.Entries())
            {
                TypedLogger.LogEntityState(this.Logger, Constants.LogKey, entry.Entity.GetType().Name, entry.IsKeySet, entry.State);
            }

            await this.Options.DbContext.SaveChangesAsync(cancellationToken).AnyContext();
        }

        return isNew ? (entity, RepositoryActionResult.Inserted) : (entity, RepositoryActionResult.Updated);

        void HandleInsert()
        {
            TypedLogger.LogUpsert(this.Logger, Constants.LogKey, "insert", typeof(TEntity).Name, entity.Id, false);

            if (entity is IConcurrency concurrencyEntity) // Set initial version before attaching
            {
                concurrencyEntity.ConcurrencyVersion = this.Options.VersionGenerator();
            }

            this.Options.DbContext.Set<TEntity>().Add(entity);
        }

        void HandleUpdate()
        {
            var isTracked = this.Options.DbContext.ChangeTracker.Entries<TEntity>().Any(e => e.Entity.Id.Equals(entity.Id));
            TypedLogger.LogUpsert(this.Logger, Constants.LogKey, "update", typeof(TEntity).Name, entity.Id, isTracked);

            if (entity is IConcurrency concurrencyEntity && this.Options.EnableOptimisticConcurrency)
            {
                var originalVersion = concurrencyEntity.ConcurrencyVersion;
                concurrencyEntity.ConcurrencyVersion = this.Options.VersionGenerator();

                if (isTracked)
                {
                    // For tracked entities, get the entry and set original version
                    this.Options.DbContext.Entry(entity).Property(nameof(IConcurrency.ConcurrencyVersion)).OriginalValue = originalVersion;
                }
                else
                {
                    // For untracked entities, attach and set original version
                    this.Options.DbContext.Update(entity); // right way to update disconnected entities https://docs.microsoft.com/en-us/ef/core/saving/disconnected-entities#working-with-graphs
                    this.Options.DbContext.Entry(entity).Property(nameof(IConcurrency.ConcurrencyVersion)).OriginalValue = originalVersion;
                }
            }
            else if (!isTracked)
            {
                this.Options.DbContext.Update(entity); // right way to update disconnected entities https://docs.microsoft.com/en-us/ef/core/saving/disconnected-entities#working-with-graphs
            }
        }
    }

    /// <summary>
    /// Deletes the entity with the provided identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="RepositoryActionResult"/> indicating the result of the delete operation.</returns>
    /// <example>
    /// var result = await repository.DeleteAsync(entityId);
    /// if (result == RepositoryActionResult.Deleted)
    /// {
    /// Console.WriteLine("Entity was successfully deleted.");
    /// }
    /// </example>
    public virtual async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var existingEntity = await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();
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
    /// Deletes the specified entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="RepositoryActionResult"/> representing the result of the delete operation.</returns>
    /// <example>
    /// Here's how you can use the DeleteAsync method:
    /// <code>
    /// var result = await repository.DeleteAsync(entity);
    /// if (result == RepositoryActionResult.Deleted)
    /// {
    /// // Entity was successfully deleted.
    /// }
    /// else
    /// {
    /// // Handle the case where the entity was not deleted.
    /// }
    /// </code>
    /// </example>
    public virtual async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null || entity.Id == default)
        {
            return RepositoryActionResult.NotFound;
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

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} repository: upsert - {EntityUpsertType} (type={EntityType}, id={EntityId}, tracked={EntityTracked})")]
        public static partial void LogUpsert(ILogger logger, string logKey, string entityUpsertType, string entityType, object entityId, bool entityTracked);

        [LoggerMessage(2, LogLevel.Trace, "{LogKey} dbcontext entity state: {EntityType} (keySet={EntityKeySet}) -> {EntityEntryState}")]
        public static partial void LogEntityState(ILogger logger, string logKey, string entityType, bool entityKeySet, EntityState entityEntryState);
    }
}