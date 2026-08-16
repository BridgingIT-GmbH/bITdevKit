// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;

/// <summary>
/// Represents generic repository domain event decorator.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
/// <param name="inner">The inner used by the operation.</param>
[Obsolete("Use GenericRepositoryDomainEventBehavior instead")]
public class GenericRepositoryDomainEventDecorator<TEntity>(
    ILoggerFactory loggerFactory,
    IGenericRepository<TEntity> inner) : RepositoryDomainEventBehavior<TEntity>(loggerFactory, inner)
    where TEntity : class, IEntity, IAggregateRoot
{ }

/// <summary>
///     <para>Decorates an <see cref="IGenericRepository{TEntity}" />.</para>
///     <para>
///         .-----------.
///         | Decorator |
///         .-----------.        .------------.
///         `------------> | decoratee  |
///         (forward)    .------------.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <seealso cref="IGenericRepository{TEntity}" />
public partial class RepositoryDomainEventBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
{
    /// <summary>
    /// Initializes a new instance of the <c>RepositoryDomainEventBehavior</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public RepositoryDomainEventBehavior(ILoggerFactory loggerFactory, IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RepositoryDomainEventBehavior<TEntity>>() ??
            NullLoggerFactory.Instance.CreateLogger<RepositoryDomainEventBehavior<TEntity>>();
        this.Inner = inner;
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Gets the inner.
    /// </summary>
    protected IGenericRepository<TEntity> Inner { get; }

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.UpdateSetAsync(set, options, cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.UpdateSetAsync(specification, set, options, cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes .
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var entity = await this.Inner
            .FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken)
            .AnyContext();
        if (entity is null || entity.Id == default)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Deletes .
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var @event = new EntityDeletedDomainEvent<TEntity>(entity);
        TypedLogger.LogRegister(this.Logger,
            Constants.LogKey,
            @event.EventId.ToString("N"),
            typeof(EntityDeletedDomainEvent<TEntity>).Name);
        entity.DomainEvents.Register(@event);

        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteSetAsync(options, cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteSetAsync(specification, options, cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteSetAsync(specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the project all operation.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="projection">The projection used by the operation.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var @event = new EntityCreatedDomainEvent<TEntity>(entity);
        TypedLogger.LogRegister(this.Logger,
            Constants.LogKey,
            @event.EventId.ToString("N"),
            typeof(EntityInsertedDomainEvent<TEntity>).Name);
        entity.DomainEvents.Register(@event);

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the insert set operation.
    /// </summary>
    /// <param name="entities">The entities involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> InsertSetAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var items = entities.SafeNull().Where(e => e is not null).ToList();

        foreach (var entity in items)
        {
            var @event = new EntityCreatedDomainEvent<TEntity>(entity);
            TypedLogger.LogRegister(this.Logger,
                Constants.LogKey,
                @event.EventId.ToString("N"),
                typeof(EntityInsertedDomainEvent<TEntity>).Name);
            entity.DomainEvents.Register(@event);
        }

        return await this.Inner.InsertSetAsync(items, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var @event = new EntityUpdatedDomainEvent<TEntity>(entity);
        TypedLogger.LogRegister(this.Logger,
            Constants.LogKey,
            @event.EventId.ToString("N"),
            typeof(EntityUpdatedDomainEvent<TEntity>).Name);
        entity.DomainEvents.Register(@event);

        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the upsert operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        DomainEventBase @event;
        if (entity.Id == default || !await this.Inner.ExistsAsync(entity.Id, cancellationToken).AnyContext())
        {
            @event = new EntityCreatedDomainEvent<TEntity>(entity);
        }
        else
        {
            @event = new EntityUpdatedDomainEvent<TEntity>(entity);
        }

        TypedLogger.LogRegister(this.Logger, Constants.LogKey, @event.EventId.ToString("N"), @event.GetType().Name);
        entity.DomainEvents.Register(@event);

        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="specifications">The specifications used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the register operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="domainEventId">The domain event id used by the operation.</param>
        /// <param name="domainEventType">The domain event type used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] repository register domain event (id={DomainEventId}, type={DomainEventType})")]
        public static partial void LogRegister(ILogger logger, string logKey, string domainEventId, string domainEventType);
    }
}
