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
    private enum WriteMode { Insert, Update, Upsert }

    private async Task<Result<TEntity>> WriteEntityAsync(TEntity entity, ActiveEntityCallbackOptions<TEntity, TId> callbacks, WriteMode mode, CancellationToken cancellationToken)
    {
        var result = await this.UpsertCoreAsync(entity, callbacks, mode, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result<TEntity>.Success(result.Value.entity) : Result<TEntity>.Failure(result);
    }

    private Task<Result<(TEntity entity, RepositoryActionResult action)>> UpsertCoreAsync(TEntity entity,
        ActiveEntityCallbackOptions<TEntity, TId> callbacks, WriteMode mode, CancellationToken cancellationToken) =>
        this.GuardAsync(() => this.session.ExecuteAsync(async () =>
        {
            if (entity is null)
            {
                return Result<(TEntity entity, RepositoryActionResult action)>.Failure(new InvalidInputError("An entity is required."));
            }

            var existing = await this.session.NativeAsync(db => LiteDbActiveEntityMapping<TId>.IsDefault(entity.Id)
                ? null : db.GetCollection<BsonDocument>(this.settings.CollectionName).FindById(LiteDbActiveEntityMapping<TId>.ToBson(entity.Id)), cancellationToken).ConfigureAwait(false);
            var isNew = existing is null;
            if (mode == WriteMode.Insert && !isNew) { throw new LiteDbProviderException(new ConflictError("The entity already exists.")); }

            if (mode == WriteMode.Update && isNew) { throw new LiteDbProviderException(new NotFoundError()); }

            await this.CallbackAsync(callbacks?.BeforeUpsertAsync, cancellationToken).ConfigureAwait(false);
            await this.CallbackAsync(isNew ? callbacks?.BeforeInsertAsync : callbacks?.BeforeUpdateAsync, cancellationToken).ConfigureAwait(false);
            await this.session.NativeAsync(db =>
            {
                var collection = db.GetCollection<BsonDocument>(this.settings.CollectionName, LiteDbActiveEntityMapping<TId>.AutoId);
                if (!isNew)
                {
                    var stored = collection.FindById(LiteDbActiveEntityMapping<TId>.ToBson(entity.Id))
                        ?? throw new LiteDbProviderException(new NotFoundError());
                    this.CheckConcurrency(entity, stored);
                }
                else if (this.settings.IdGenerator?.IsNew(entity.Id) == true)
                {
                    this.settings.IdGenerator.SetNew(entity);
                }

                this.AdvanceVersion(entity);
                var document = db.Mapper.ToDocument(entity);
                if (isNew)
                {
                    LiteDbActiveEntityMapping<TId>.PrepareId(entity, document);
                    if (document.ContainsKey("_id") && collection.FindById(document["_id"]) is not null)
                    {
                        throw new LiteDbProviderException(new ConflictError("The entity already exists."));
                    }

                    var id = collection.Insert(document);
                    entity.Id = LiteDbActiveEntityMapping<TId>.FromBson(id);
                }
                else if (!collection.Update(document))
                {
                    throw new LiteDbProviderException(new NotFoundError());
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
            await this.CallbackAsync(isNew ? callbacks?.AfterInsertAsync : callbacks?.AfterUpdateAsync, cancellationToken).ConfigureAwait(false);
            await this.CallbackAsync(callbacks?.AfterUpsertAsync, cancellationToken).ConfigureAwait(false);
            await this.session.NativeAsync(db =>
            {
                var persisted = db.GetCollection<BsonDocument>(this.settings.CollectionName).FindById(LiteDbActiveEntityMapping<TId>.ToBson(entity.Id));
                this.session.CaptureMutation(db, entity, existing, persisted, this.settings.CollectionName, isNew ? "Insert" : "Update", "ActiveEntity");
                return true;
            }, cancellationToken).ConfigureAwait(false);
            return Result<(TEntity entity, RepositoryActionResult action)>.Success((entity, isNew ? RepositoryActionResult.Inserted : RepositoryActionResult.Updated));
        }, true, cancellationToken));

    private async Task CallbackAsync(Func<IActiveEntityEntityProvider<TEntity, TId>, CancellationToken, Task<Result>> callback, CancellationToken cancellationToken)
    {
        if (callback is not null)
        {
            var result = await callback(this, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) { throw new LiteDbProviderException(result); }
        }
    }

    private void CheckConcurrency(TEntity entity, BsonDocument stored)
    {
        if (this.settings.EnableOptimisticConcurrency && entity is IConcurrency concurrency)
        {
            var current = this.session.Mapper.ToObject<TEntity>(stored) as IConcurrency;
            if (current!.ConcurrencyVersion != concurrency.ConcurrencyVersion)
            {
                throw new LiteDbProviderException(new ConcurrencyError("The stored entity has a different concurrency version."));
            }
        }
    }

    private void AdvanceVersion(TEntity entity)
    {
        if (entity is IConcurrency concurrency)
        {
            this.session.TrackVersion(concurrency);
            concurrency.ConcurrencyVersion = this.settings.VersionGenerator();
        }
    }
}
