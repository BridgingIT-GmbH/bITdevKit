// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Provides atomic LiteDB repository writes with scoped transactions, concurrency, and feature capture.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="options">The repository configuration.</param>
public class LiteDbGenericRepository<TEntity>(ILiteDbRepositoryOptions options)
    : LiteDbReadOnlyGenericRepository<TEntity>(options), IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Creates a repository through the existing options builder.</summary>
    /// <param name="optionsBuilder">The configuration callback.</param>
    public LiteDbGenericRepository(Builder<LiteDbRepositoryOptionsBuilder, LiteDbRepositoryOptions> optionsBuilder)
        : this(optionsBuilder(new LiteDbRepositoryOptionsBuilder()).Build()) { }

    /// <inheritdoc />
    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        (await this.UpsertAsync(entity, cancellationToken).ConfigureAwait(false)).entity;

    /// <inheritdoc />
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        (await this.UpsertAsync(entity, cancellationToken).ConfigureAwait(false)).entity;

    /// <inheritdoc />
    public virtual Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db => this.Store.Upsert(db, entity), true, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IEnumerable<TEntity>> InsertSetAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync<IEnumerable<TEntity>>(db =>
        {
            var items = Materialize(entities, this.Options.MaxMaterializedRows, cancellationToken);
            var operationId = Guid.NewGuid();
            foreach (var entity in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.Store.Upsert(db, entity, source: "InsertSet", operationId: operationId, affectedEntityCount: items.Count);
            }

            return items;
        }, true, cancellationToken);

    /// <inheritdoc />
    public virtual Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db => this.Store.Delete(db, id), true, cancellationToken);

    /// <inheritdoc />
    public virtual Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db => entity is null ? RepositoryActionResult.None : this.Store.Delete(db, entity.Id, entity), true, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> UpdateSetAsync(Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.UpdateSetAsync([], set, options, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> UpdateSetAsync(ISpecification<TEntity> specification, Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.UpdateSetAsync([specification], set, options, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> UpdateSetAsync(IEnumerable<ISpecification<TEntity>> specifications, Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db =>
        {
            ArgumentNullException.ThrowIfNull(set);
            var updates = new EntityUpdateSet<TEntity>();
            set(updates);
            var targets = this.Store.Query.Select(db, specifications, options, false, cancellationToken).Items;
            var operationId = Guid.NewGuid();
            foreach (var entity in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = this.Store.Identity(entity.Id);
                updates.Apply(entity);
                if (id != this.Store.Identity(entity.Id)) { throw new LiteDbProviderException(new InvalidInputError("Set updates cannot change identities.")); }

                this.Store.Upsert(db, entity, source: "UpdateSet", operationId: operationId, affectedEntityCount: targets.Count);
            }

            return (long)targets.Count;
        }, true, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> DeleteSetAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.DeleteSetAsync([], options, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> DeleteSetAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.DeleteSetAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public virtual Task<long> DeleteSetAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.Store.ExecuteAsync(db =>
        {
            var targets = this.Store.Query.Select(db, specifications, options, false, cancellationToken).Items;
            var operationId = Guid.NewGuid();
            foreach (var entity in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.Store.Delete(db, entity.Id, entity, "DeleteSet", operationId, targets.Count);
            }

            return (long)targets.Count;
        }, true, cancellationToken);

    /// <summary>Materializes a bounded entity sequence before writing, ignoring null entries.</summary>
    /// <param name="entities">The input sequence.</param>
    /// <param name="maximum">The configured row limit.</param>
    /// <param name="cancellationToken">Cancels enumeration.</param>
    /// <returns>The validated materialized selection.</returns>
    public static IReadOnlyList<TEntity> Materialize(IEnumerable<TEntity> entities, int maximum, CancellationToken cancellationToken)
    {
        var result = new List<TEntity>();
        foreach (var entity in entities ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entity is null) { continue; }

            if (result.Count == maximum) { throw new LiteDbProviderException(new InvalidOperationError("The write exceeds the configured row limit.")); }

            result.Add(entity);
        }

        return result;
    }
}
