// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

public sealed partial class LiteDbActiveEntityProvider<TEntity, TId>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public Task<Result<long>> UpdateSetAsync(ISpecification<TEntity> specification, Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.UpdateSetAsync([specification], set, options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> UpdateSetAsync(IEnumerable<ISpecification<TEntity>> specifications, Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.SetAsync(specifications, set, options, false, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> DeleteSetAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.DeleteSetAsync([specification], options, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> DeleteSetAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default) =>
        this.SetAsync(specifications, null, options, true, cancellationToken);

    private Task<Result<long>> SetAsync(IEnumerable<ISpecification<TEntity>> specifications, Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options, bool delete, CancellationToken cancellationToken) =>
        this.GuardAsync(() => this.session.ExecuteAsync(async () =>
        {
            if (!delete && set is null) { throw new LiteDbProviderException(new InvalidInputError("An update set is required.")); }

            var updates = new EntityUpdateSet<TEntity>();
            set?.Invoke(updates);
            var selection = await this.session.NativeAsync(db => this.query.Select(db, specifications, options, false, cancellationToken), cancellationToken).ConfigureAwait(false);
            var operationId = Guid.NewGuid();
            long affected = 0;
            foreach (var batch in selection.Items.Chunk(128))
            {
                affected += await this.session.NativeAsync(db =>
                {
                    var collection = db.GetCollection<BsonDocument>(this.settings.CollectionName);
                    foreach (var entity in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var id = LiteDbActiveEntityMapping<TId>.ToBson(entity.Id);
                        var before = collection.FindById(id);
                        if (delete)
                        {
                            if (!collection.Delete(id)) { throw new LiteDbProviderException(new NotFoundError()); }
                        }
                        else
                        {
                            updates.Apply(entity);
                            if (LiteDbActiveEntityMapping<TId>.ToBson(entity.Id) != id)
                            {
                                throw new LiteDbProviderException(new InvalidInputError("Set updates cannot change entity identities."));
                            }

                            this.AdvanceVersion(entity);
                            if (!collection.Update(db.Mapper.ToDocument(entity))) { throw new LiteDbProviderException(new NotFoundError()); }
                        }

                        this.session.CaptureMutation(db, entity, before, delete ? null : collection.FindById(id), this.settings.CollectionName,
                            delete ? "Delete" : "Update", delete ? "DeleteSet" : "UpdateSet", operationId, selection.Items.Count);
                    }

                    return (long)batch.Length;
                }, cancellationToken).ConfigureAwait(false);
            }

            return Result<long>.Success(affected);
        }, true, cancellationToken));
}
