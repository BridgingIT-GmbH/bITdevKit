// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

/// <summary>
/// Represents generic repository tracing decorator.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="inner">The inner used by the operation.</param>
[Obsolete("Use GenericRepositoryTracingBehavior instead")]
public class GenericRepositoryTracingDecorator<TEntity>(IGenericRepository<TEntity> inner)
    : RepositoryTracingBehavior<TEntity>(inner)
    where TEntity : class, IEntity
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
public class RepositoryTracingBehavior<TEntity>(IGenericRepository<TEntity> inner) : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly string type = typeof(TEntity).Name;

    /// <summary>
    /// Gets the inner.
    /// </summary>
    protected IGenericRepository<TEntity> Inner { get; } = inner;

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY UpdateSet {this.type}",
            async (a, c) => await this.Inner.UpdateSetAsync(set, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY UpdateSet {this.type}",
            async (a, c) => await this.Inner.UpdateSetAsync(specification, set, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY UpdateSet {this.type}",
            async (a, c) => await this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY Count {this.type}",
            async (a, c) => await this.Inner.CountAsync(specifications, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
    /// Deletes .
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY Delete {this.type}",
            async (a, c) => await this.Inner.DeleteAsync(id, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes .
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY Delete {this.type}",
            async (a, c) => await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY DeleteSet {this.type}",
            async (a, c) => await this.Inner.DeleteSetAsync(options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY DeleteSet {this.type}",
            async (a, c) => await this.Inner.DeleteSetAsync(specification, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY DeleteSet {this.type}",
            async (a, c) => await this.Inner.DeleteSetAsync(specifications, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY Exists {this.type}",
            async (a, c) => await this.Inner.ExistsAsync(id, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY FindAll {this.type}",
            async (a, c) => await this.Inner.FindAllAsync(options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY FindAll {this.type}",
            async (a, c) => await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY FindAll {this.type}",
            async (a, c) => await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY ProjectAll {this.type}",
            async (a, c) => await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY ProjectAll {this.type}",
            async (a, c) => await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken)
                .AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY ProjectAll {this.type}",
            async (a, c) => await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken)
                .AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY FindOne {this.type}",
            async (a, c) => await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY FindOne {this.type}",
            async (a, c) => await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY FindOne {this.type}",
            async (a, c) => await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY Insert {this.type}",
            async (a, c) => await this.Inner.InsertAsync(entity, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY InsertSet {this.type}",
            async (a, c) => await this.Inner.InsertSetAsync(entities, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes the update operation.
    /// </summary>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await Activity.Current.StartActvity($"REPOSITORY Update {this.type}",
            async (a, c) => await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
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
        return await Activity.Current.StartActvity($"REPOSITORY Upsert {this.type}",
            async (a, c) => await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext(),
            cancellationToken: cancellationToken);
    }
}
