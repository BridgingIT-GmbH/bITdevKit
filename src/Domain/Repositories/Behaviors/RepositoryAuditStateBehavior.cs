// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
public partial class RepositoryAuditStateBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    private readonly string type = typeof(TEntity).Name;
    private readonly RepositoryAuditStateBehaviorOptions options;
    private readonly ICurrentUserAccessor currentUserAccessor;

    /// <summary>
    /// Initializes a new instance of the <c>RepositoryAuditStateBehavior</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="ínner">The repository being decorated.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    public RepositoryAuditStateBehavior(
        ILoggerFactory loggerFactory,
        IGenericRepository<TEntity> ínner,
        RepositoryAuditStateBehaviorOptions options = null,
        ICurrentUserAccessor currentUserAccessor = null)
        : this(ínner)
    {
        this.Logger = loggerFactory?.CreateLogger<IGenericRepository<TEntity>>() ??
            NullLoggerFactory.Instance.CreateLogger<IGenericRepository<TEntity>>();
        this.options = options ?? new RepositoryAuditStateBehaviorOptions();
        this.currentUserAccessor = currentUserAccessor ?? new NullCurrentUserAccessor();
    }

    /// <summary>
    /// Initializes a new instance of the <c>RepositoryAuditStateBehavior</c> class.
    /// </summary>
    /// <param name="ínner">The repository being decorated.</param>
    public RepositoryAuditStateBehavior(IGenericRepository<TEntity> ínner)
    {
        EnsureArg.IsNotNull(ínner, nameof(ínner));

        this.Inner = ínner;
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
        if (id != default && this.options.SoftDeleteEnabled)
        {
            var entity = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken).AnyContext();
            return await this.DeleteAsync(entity, cancellationToken);
        }

        return await this.Inner.DeleteAsync(id, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Deletes .
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is not null && this.options.SoftDeleteEnabled)
        {
            entity.AuditState ??= new AuditState();
            entity.AuditState.SetDeleted(this.GetByValue());

            TypedLogger.LogSoftDelete(this.Logger, Constants.LogKey, this.type, entity.Id);

            var result = (await this.UpsertAsync(entity, cancellationToken).AnyContext()).action;
            if (result == RepositoryActionResult.Updated)
            {
                return RepositoryActionResult.Deleted;
            }

            return result;
        }

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
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        var entity = await this.Inner.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();
        if (entity is not null && this.options.SoftDeleteEnabled)
        {
            var notDeletedSpecification = new Specification<TEntity>(e => !e.AuditState.Deleted.HasValue || !e.AuditState.Deleted.Value);
            return notDeletedSpecification.IsSatisfiedBy(entity);
        }

        return entity is not null;
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(new List<ISpecification<TEntity>>([specification]), options, cancellationToken).AnyContext();
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
        if (this.options.SoftDeleteEnabled)
        {
            var notDeletedSpecification = new Specification<TEntity>(e => !e.AuditState.Deleted.HasValue || !e.AuditState.Deleted.Value);
            specifications = specifications.SafeNull().Concat([notDeletedSpecification]);
        }

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
        return await this.ProjectAllAsync([], projection, options, cancellationToken).AnyContext();
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
        return await this.ProjectAllAsync(new List<ISpecification<TEntity>>([specification]), projection, options, cancellationToken).AnyContext();
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
        if (this.options.SoftDeleteEnabled)
        {
            var notDeletedSpecification = new Specification<TEntity>(e => !e.AuditState.Deleted.HasValue || !e.AuditState.Deleted.Value);
            specifications = specifications.SafeNull().Concat([notDeletedSpecification]);
        }

        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="id">The entity identifier.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
        if (entity != null && this.options.SoftDeleteEnabled)
        {
            var notDeletedSpecification = new Specification<TEntity>(e => !e.AuditState.Deleted.HasValue || !e.AuditState.Deleted.Value);
            return notDeletedSpecification.IsSatisfiedBy(entity) ? entity : default;
        }

        return entity;
    }

    /// <summary>
    /// Finds one.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="options">The options controlling the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync(new List<ISpecification<TEntity>>([specification]), options, cancellationToken).AnyContext();
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
        if (this.options.SoftDeleteEnabled)
        {
            var notDeletedSpecification = new Specification<TEntity>(e => !e.AuditState.Deleted.HasValue || !e.AuditState.Deleted.Value);
            specifications = specifications.SafeNull().Concat([notDeletedSpecification]);
        }

        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetCreated(this.GetByValue());

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Executes the insert set operation.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
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
            entity.AuditState ??= new AuditState();
            entity.AuditState.SetCreated(this.GetByValue());
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
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetUpdated(this.GetByValue());

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
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetUpdated(this.GetByValue());

        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
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
    /// <param name="specification">The specification used to filter entities.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new List<ISpecification<TEntity>>([specification]), cancellationToken).AnyContext();
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
        if (this.options.SoftDeleteEnabled)
        {
            var notDeletedSpecification = new Specification<TEntity>(e => !e.AuditState.Deleted.HasValue || !e.AuditState.Deleted.Value);
            specifications = specifications.SafeNull().Concat([notDeletedSpecification]);
        }

        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    private string GetByValue()
    {
        switch (this.options.ByType)
        {
            case AuditStateByType.ByUserName:
                return this.currentUserAccessor.UserName;
            case AuditStateByType.ByEmail:
                return this.currentUserAccessor.Email;
            case AuditStateByType.ByUserId:
                break;
            default:
                return this.currentUserAccessor.UserId;
        }

        return this.currentUserAccessor.UserId;
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the soft delete operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="entityType">The name of the entity type.</param>
        /// <param name="entityId">The entity identifier.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] repository: soft delete (type={EntityType}, id={EntityId})")]
        public static partial void LogSoftDelete(ILogger logger, string logKey, string entityType, object entityId);
    }
}

/// <summary>
/// Defines the supported audit state by type values.
/// </summary>
public enum AuditStateByType
{
    /// <summary>
    /// Represents the by user id value.
    /// </summary>
    ByUserId = 0,
    /// <summary>
    /// Represents the by user name value.
    /// </summary>
    ByUserName = 1,
    /// <summary>
    /// Represents the by email value.
    /// </summary>
    ByEmail = 2
}
