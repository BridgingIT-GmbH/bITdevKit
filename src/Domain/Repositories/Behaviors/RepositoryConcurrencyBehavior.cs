// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

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
//[Obsolete("Concurrency behavior is built in in repos")]
public class RepositoryConcurrencyBehavior<TEntity>(IGenericRepository<TEntity> inner) : IGenericRepository<TEntity>
    where TEntity : class, IEntity, IConcurrency
{
    protected IGenericRepository<TEntity> Inner { get; } = inner;

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
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        var entity = await this.FindOneAsync(id, new FindOptions<TEntity> { NoTracking = false }, cancellationToken).AnyContext();
        if (entity is not null)
        {
            return await this.DeleteAsync(entity, cancellationToken).AnyContext();
        }

        return RepositoryActionResult.None;
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.ConcurrencyVersion = GuidGenerator.CreateSequential();

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

    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
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
        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
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

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.ConcurrencyVersion = GuidGenerator.CreateSequential();

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        entity.ConcurrencyVersion = GuidGenerator.CreateSequential();

        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        entity.ConcurrencyVersion = GuidGenerator.CreateSequential();

        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specification, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }
}
