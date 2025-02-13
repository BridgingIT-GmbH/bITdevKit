// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

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

    public RepositoryAuditStateBehavior(IGenericRepository<TEntity> ínner)
    {
        EnsureArg.IsNotNull(ínner, nameof(ínner));

        this.Inner = ínner;
    }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    protected IGenericRepository<TEntity> Inner { get; }

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var entity = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken).AnyContext();
        if (entity is not null && this.options.SoftDeleteEnabled)
        {
            entity.AuditState ??= new AuditState();
            entity.AuditState.SetDeleted(this.GetByValue());

            TypedLogger.LogSoftDelete(this.Logger, Constants.LogKey, this.type, id);

            var result = (await this.UpsertAsync(entity, cancellationToken).AnyContext()).action;
            if (result == RepositoryActionResult.Updated)
            {
                return RepositoryActionResult.Deleted;
            }

            return result;
        }

        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
    }

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

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync([], options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(new List<ISpecification<TEntity>>([specification]), options, cancellationToken).AnyContext();
    }

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

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync([], projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync(new List<ISpecification<TEntity>>([specification]), projection, options, cancellationToken).AnyContext();
    }

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

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync([], options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync(new List<ISpecification<TEntity>>([specification]), options, cancellationToken).AnyContext();
    }

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

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetCreated(this.GetByValue());

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetUpdated(this.GetByValue());

        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        entity.AuditState ??= new AuditState();
        entity.AuditState.SetUpdated(this.GetByValue());

        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new List<ISpecification<TEntity>>([specification]), cancellationToken).AnyContext();
    }

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

    public static partial class TypedLogger
    {
        [LoggerMessage(1, LogLevel.Information, "{LogKey} repository: soft delete (type={EntityType}, id={EntityId})")]
        public static partial void LogSoftDelete(ILogger logger, string logKey, string entityType, object entityId);
    }
}

public enum AuditStateByType
{
    ByUserId = 0,
    ByUserName = 1,
    ByEmail = 2
}