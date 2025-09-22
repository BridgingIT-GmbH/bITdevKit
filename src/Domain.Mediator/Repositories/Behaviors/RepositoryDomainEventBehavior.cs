// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;

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
    public RepositoryDomainEventBehavior(ILoggerFactory loggerFactory, IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RepositoryDomainEventBehavior<TEntity>>() ??
            NullLoggerFactory.Instance.CreateLogger<RepositoryDomainEventBehavior<TEntity>>();
        this.Inner = inner;
    }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    protected IGenericRepository<TEntity> Inner { get; }

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

        var @event = new EntityCreatedDomainEvent<TEntity>(entity);
        TypedLogger.LogRegister(this.Logger,
            Constants.LogKey,
            @event.EventId.ToString("N"),
            typeof(EntityInsertedDomainEvent<TEntity>).Name);
        entity.DomainEvents.Register(@event);

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

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

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} repository register domain event (id={DomainEventId}, type={DomainEventType})")]
        public static partial void LogRegister(ILogger logger, string logKey, string domainEventId, string domainEventType);
    }
}