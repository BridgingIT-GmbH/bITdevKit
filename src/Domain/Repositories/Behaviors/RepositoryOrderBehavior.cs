// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

[Obsolete("Use GenericRepositoryOrderBehavior instead")]
public class GenericRepositoryOrderDecorator<TEntity> : RepositoryOrderBehavior<TEntity>
    where TEntity : class, IEntity
{
    public GenericRepositoryOrderDecorator(
        Expression<Func<TEntity, object>> expression,
        IGenericRepository<TEntity> inner)
        : base(expression, inner) { }

    public GenericRepositoryOrderDecorator(
        Expression<Func<TEntity, object>> expression,
        OrderDirection direction,
        IGenericRepository<TEntity> inner)
        : base(expression, direction, inner) { }
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
public class RepositoryOrderBehavior<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public RepositoryOrderBehavior(
        Expression<Func<TEntity, object>> expression, // TODO: also accept a proper OrderOption collection
        IGenericRepository<TEntity> inner)
        : this(expression, OrderDirection.Ascending, inner) { }

    public RepositoryOrderBehavior(
        Expression<Func<TEntity, object>> expression, // TODO: also accept a proper OrderOption collection
        OrderDirection direction,
        IGenericRepository<TEntity> inner)
    {
        EnsureArg.IsNotNull(expression, nameof(expression));

        this.Expression = expression;
        this.Direction = direction;
        this.Inner = inner;
    }

    protected IGenericRepository<TEntity> Inner { get; }

    protected Expression<Func<TEntity, object>> Expression { get; }

    protected OrderDirection Direction { get; }

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteAsync(id, cancellationToken).AnyContext();
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

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
        options = this.EnsureOptions(options);

        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        options = this.EnsureOptions(options);

        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
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

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new[] { specification }, cancellationToken).AnyContext();
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

    private IFindOptions<TEntity> EnsureOptions(IFindOptions<TEntity> options)
    {
        if (options is null)
        {
            options = new FindOptions<TEntity>();
        }

        options.Orders = options.Orders.Insert(new OrderOption<TEntity>(this.Expression, this.Direction));

        return options;
    }
}