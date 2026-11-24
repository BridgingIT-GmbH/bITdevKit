// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;

/// <summary>Provides detached LiteDB entity queries using a supplied context or shared database session.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class LiteDbReadOnlyGenericRepository<TEntity> : IGenericReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Creates a read-only repository.</summary>
    /// <param name="options">The context or scoped session configuration.</param>
    public LiteDbReadOnlyGenericRepository(ILiteDbRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.Options = options;
        this.Store = new LiteDbRepositoryStore<TEntity>(options);
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
    }

    /// <summary>Creates a read-only repository through the existing options builder.</summary>
    /// <param name="optionsBuilder">The configuration callback.</param>
    public LiteDbReadOnlyGenericRepository(Builder<LiteDbRepositoryOptionsBuilder, LiteDbRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new LiteDbRepositoryOptionsBuilder()).Build()) { }

    /// <summary>Gets the shared native repository mechanics.</summary>
    protected LiteDbRepositoryStore<TEntity> Store { get; }

    /// <summary>Gets the frozen repository configuration.</summary>
    protected ILiteDbRepositoryOptions Options { get; }

    /// <summary>Gets the repository logger.</summary>
    protected ILogger<IGenericRepository<TEntity>> Logger { get; }

    /// <inheritdoc />
    public virtual Task<IEnumerable<TEntity>> FindAllAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllAsync([], options, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IEnumerable<TEntity>> FindAllAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindAllAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        (await this.Store.ExecuteAsync(db => this.Store.Query.Select(db, specifications, options, false, cancellationToken), false, cancellationToken).ConfigureAwait(false)).Items;

    /// <inheritdoc />
    public virtual Task<TEntity> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db => this.Store.Find(db, id, options), false, cancellationToken);

    /// <inheritdoc />
    public virtual Task<TEntity> FindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.FindOneAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public virtual async Task<TEntity> FindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        (await this.Store.ExecuteAsync(db => this.Store.Query.Select(db, specifications, FirstOptions(options), false, cancellationToken), false, cancellationToken).ConfigureAwait(false)).Items.FirstOrDefault();

    /// <inheritdoc />
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllAsync([], projection, options, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.ProjectAllAsync([specification], projection, options, cancellationToken);

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var entities = await this.FindAllAsync(specifications, options, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return entities.Select(projection.Compile()).ToArray();
    }

    /// <inheritdoc />
    public virtual Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db => id is not null && db.GetCollection<BsonDocument>(this.Store.CollectionName).FindById(this.Store.Identity(id)) is not null, false, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> CountAsync(CancellationToken cancellationToken = default) => this.CountAsync([], cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        this.CountAsync([specification], cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db => this.Store.Query.Count(db, specifications, null, cancellationToken), false, cancellationToken);

    private static FindOptions<TEntity> FirstOptions(IFindOptions<TEntity> options) => new()
    {
        Skip = options?.Skip, Take = options?.Take <= 0 ? options.Take : 1, Distinct = options?.Distinct,
        Order = options?.Order, Orders = options?.Orders, Include = options?.Include,
        Includes = options?.Includes, Hierarchy = options?.Hierarchy, NoTracking = options?.NoTracking ?? false
    };
}
