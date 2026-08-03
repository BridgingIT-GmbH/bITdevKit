// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Domain.Outbox;
using BridgingIT.DevKit.Domain;

[Obsolete("Use RepositoryOutboxDomainEventBehavior instead")]
/// <summary>
/// Backwards-compatible alias for <see cref="RepositoryOutboxDomainEventBehavior{TEntity,TContext}" />.
/// </summary>
public class GenericRepositoryDomainEventOutboxDecorator<TEntity, TContext>(
    ILoggerFactory loggerFactory,
    TContext context,
    IGenericRepository<TEntity> inner,
    IOutboxDomainEventQueue eventQueue = null,
    OutboxDomainEventOptions options = null)
    : RepositoryOutboxDomainEventBehavior<TEntity, TContext>(loggerFactory, context, inner, eventQueue, options)
    where TEntity : class, IEntity, IAggregateRoot
    where TContext : DbContext, IOutboxDomainEventContext
{ }

/// <summary>
/// Decorates a repository so domain events raised by aggregates are stored in the Entity Framework outbox.
/// </summary>
public partial class RepositoryOutboxDomainEventBehavior<TEntity, TContext> : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
    where TContext : DbContext, IOutboxDomainEventContext
{
    private readonly IOutboxDomainEventQueue eventQueue;
    private readonly OutboxDomainEventOptions options;
    private readonly OutboxDomainEventCollector collector;

    public RepositoryOutboxDomainEventBehavior(
        ILoggerFactory loggerFactory,
        TContext context,
        IGenericRepository<TEntity> inner,
        IOutboxDomainEventQueue eventQueue = null,
        OutboxDomainEventOptions options = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RepositoryOutboxDomainEventBehavior<TEntity, TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<RepositoryOutboxDomainEventBehavior<TEntity, TContext>>();
        this.Context = context;
        this.Inner = inner;
        this.eventQueue = eventQueue;
        this.options = options ?? new OutboxDomainEventOptions();
        this.options.Serializer ??= new SystemTextJsonSerializer();
        this.collector = new OutboxDomainEventCollector(this.options);
    }

    /// <summary>
    /// Gets the logger used for repository outbox diagnostics.
    /// </summary>
    protected ILogger<RepositoryOutboxDomainEventBehavior<TEntity, TContext>> Logger { get; }

    /// <summary>
    /// Gets the DbContext that stores persisted outbox rows.
    /// </summary>
    protected TContext Context { get; }

    /// <summary>
    /// Gets the decorated repository implementation.
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

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var existingEntity = await this.Inner
            .FindOneAsync(id, new FindOptions<TEntity> { NoTracking = true }, cancellationToken).AnyContext();
        if (existingEntity is null || existingEntity.Id == default)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(existingEntity, cancellationToken);
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
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

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.InsertAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<IEnumerable<TEntity>> InsertSetAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var items = entities.SafeNull().Where(e => e is not null).ToList();
        var result = await this.Inner.InsertSetAsync(items, cancellationToken).AnyContext();

        foreach (var entity in items)
        {
            await this.StoreDomainEvents(entity, cancellationToken);
            entity.DomainEvents.Clear();
        }

        return result;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));

        var result = await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext(); // calls savechanges, acts as a transaction for all inserts that are part of the set.

        await this.StoreDomainEvents(entity, cancellationToken);

        entity.DomainEvents.Clear();

        return result;
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    private async Task StoreDomainEvents(TEntity entity, CancellationToken cancellationToken)
    {
        var projections = this.collector.Collect([entity]);
        projections.ForEach(projection =>
                {
                    TypedLogger.LogDomainEvent(
                        this.Logger,
                        Constants.LogKey,
                        projection.DomainEvent.EventId,
                        projection.DomainEvent.GetType().Name);
                    this.Context.OutboxDomainEvents.Add(projection.OutboxEvent);
#if DEBUG
                    //this.Logger.LogDebug("++++ OUTBOX: STORE DOMAINEVENT {@DomainEvent}", projection.OutboxEvent);
#endif
                }, cancellationToken);

        if (this.options.AutoSave)
        {
            await this.Context.SaveChangesAsync<OutboxDomainEvent>(this.Logger, cancellationToken).AnyContext(); // only save changes in this scoped context
        }

        if (this.options.ProcessingMode == OutboxDomainEventProcessMode.Immediate)
        {
            projections.ForEach(
                projection => this.eventQueue?.Enqueue(projection.OutboxEvent.EventId),
                cancellationToken);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] repository outbox domain event (eventId={DomainEventId}, eventType={DomainEventType})")]
        public static partial void LogDomainEvent(ILogger logger, string logKey, Guid domainEventId, string domainEventType);
    }
}
