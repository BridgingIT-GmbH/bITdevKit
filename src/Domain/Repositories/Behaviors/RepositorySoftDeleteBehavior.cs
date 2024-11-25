// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

[Obsolete("Use GenericRepositorySoftDeleteBehavior instead")]
public class GenericRepositorySoftDeleteDecorator<TEntity> : RepositorySoftDeleteBehavior<TEntity>
    where TEntity : class, IEntity, ISoftDeletable
{
    public GenericRepositorySoftDeleteDecorator(IGenericRepository<TEntity> ínner)
        : base(ínner) { }

    public GenericRepositorySoftDeleteDecorator(IGenericRepository<TEntity> ínner, bool excludeDeleted)
        : base(ínner, excludeDeleted) { }
}

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
public class RepositorySoftDeleteBehavior<TEntity>(IGenericRepository<TEntity> ínner, bool excludeDeleted)
    : IGenericRepository<TEntity>
    where TEntity : class, IEntity, ISoftDeletable
{
    public RepositorySoftDeleteBehavior(IGenericRepository<TEntity> ínner)
        : this(ínner, true) { }

    protected IGenericRepository<TEntity> Inner { get; } = ínner;

    protected ISpecification<TEntity> Specification { get; } =
        excludeDeleted ? new Specification<TEntity>(e => e.Deleted != true) : null;

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var entity = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken)
            .AnyContext();
        if (entity is not null)
        {
            entity.SetDeleted();

            var result = (await this.UpsertAsync(entity, cancellationToken).AnyContext()).action;
            if (result == RepositoryActionResult.Updated)
            {
                return RepositoryActionResult.Deleted;
            }
        }

        return RepositoryActionResult.None;
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.DeleteAsync(entity?.Id, cancellationToken).AnyContext();
    }

    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        var entity = await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext();

        return entity is not null && this.Specification.IsSatisfiedBy(entity);
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
        return await this.FindAllAsync(new List<ISpecification<TEntity>>([specification]),
                options,
                cancellationToken)
            .AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindAllAsync(new[] { this.Specification }.Concat(specifications.SafeNull()),
                options,
                cancellationToken)
            .AnyContext();
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
        return await this.ProjectAllAsync(new List<ISpecification<TEntity>>([specification]),
                projection,
                options,
                cancellationToken)
            .AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.ProjectAllAsync(new[] { this.Specification }.Concat(specifications.SafeNull()),
                projection,
                options,
                cancellationToken)
            .AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();

        return entity is not null && this.Specification.IsSatisfiedBy(entity) ? entity : default;
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(this.Specification.And(specification), options, cancellationToken)
            .AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.FindOneAsync(new[] { this.Specification }.Concat(specifications.SafeNull()),
                options,
                cancellationToken)
            .AnyContext();
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
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
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner
            .CountAsync(new[] { this.Specification }.Concat(specifications.SafeNull()), cancellationToken)
            .AnyContext();
    }
}