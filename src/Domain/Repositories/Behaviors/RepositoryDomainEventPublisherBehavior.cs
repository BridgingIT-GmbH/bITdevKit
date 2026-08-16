// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;

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
public class RepositoryDomainEventPublisherBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IAggregateRoot
{
    /// <summary>
    /// Initializes a new instance of the <c>RepositoryDomainEventPublisherBehavior</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="notifier">The notifier used by the operation.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public RepositoryDomainEventPublisherBehavior(
        ILoggerFactory loggerFactory,
        INotifier notifier,
        IGenericRepository<TEntity> inner)
        : this(loggerFactory, new NotifierDomainEventPublisher(loggerFactory, notifier), inner) { }

    /// <summary>
    /// Initializes a new instance of the <c>RepositoryDomainEventPublisherBehavior</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="publisher">The publisher used by the operation.</param>
    /// <param name="inner">The inner used by the operation.</param>
    public RepositoryDomainEventPublisherBehavior(
        ILoggerFactory loggerFactory,
        IDomainEventPublisher publisher,
        IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(publisher, nameof(publisher));
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RepositoryDomainEventPublisherBehavior<TEntity>>() ??
            NullLoggerFactory.Instance.CreateLogger<RepositoryDomainEventPublisherBehavior<TEntity>>();
        this.Publisher = publisher;
        this.Inner = inner;
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <summary>
    /// Gets the publisher.
    /// </summary>
    protected IDomainEventPublisher Publisher { get; }

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
            .FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken).AnyContext();
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

        var result = await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();

        // publish all domain events after transaction ends
        await entity.DomainEvents.GetAll()
            .ForEachAsync(async e => { await this.Publisher.Send(e, cancellationToken).AnyContext(); }, cancellationToken);
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

        var result = await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();

        // publish all domain events after transaction ends
        await entity.DomainEvents.GetAll()
            .ForEachAsync(async e => { await this.Publisher.Send(e, cancellationToken).AnyContext(); }, cancellationToken);
        entity.DomainEvents.Clear();

        return result;
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
        var result = await this.Inner.InsertSetAsync(items, cancellationToken).AnyContext();

        foreach (var entity in items)
        {
            await entity.DomainEvents.GetAll()
                .ForEachAsync(async e => { await this.Publisher.Send(e, cancellationToken).AnyContext(); }, cancellationToken);
            entity.DomainEvents.Clear();
        }

        return result;
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

        var result = await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();

        // publish all domain events after transaction ends
        await entity.DomainEvents.GetAll()
            .ForEachAsync(async e => { await this.Publisher.Send(e, cancellationToken).AnyContext(); }, cancellationToken);
        entity.DomainEvents.Clear();

        return result;
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

        var result = await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();

        // publish all domain events after transaction ends
        await entity.DomainEvents.GetAll()
            .ForEachAsync(async e => { await this.Publisher.Send(e, cancellationToken).AnyContext(); }, cancellationToken);
        entity.DomainEvents.Clear();

        return result;
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
}
