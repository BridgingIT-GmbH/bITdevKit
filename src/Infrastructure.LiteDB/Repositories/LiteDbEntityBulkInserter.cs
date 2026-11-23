// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Inserts a bounded native batch atomically without implicit entity behaviors.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="options">The selected scoped session and collection.</param>
public sealed class LiteDbEntityBulkInserter<TEntity>(ILiteDbRepositoryOptions options) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
{
    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var items = new List<TEntity>();
            foreach (var entity in entities ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entity is null) { return Result<long>.Failure(new InvalidInputError("A bulk insert cannot contain null entities.")); }

                if (items.Count == options.MaxMaterializedRows) { return Result<long>.Failure(new InvalidOperationError("The bulk insert exceeds the configured row limit.")); }

                items.Add(entity);
            }

            if (items.Count == 0) { return Result<long>.Success(0); }

            var identity = new LiteDbEntityIdentity(typeof(TEntity).GetProperty("Id")!.PropertyType);
            var store = new LiteDbRepositoryStore<TEntity>(options);
            var count = await store.ExecuteAsync(database =>
            {
                var documents = items.Select(entity =>
                {
                    var document = database.Mapper.ToDocument(entity);
                    identity.Prepare(entity, document);
                    return document;
                }).ToArray();
                var collection = database.GetCollection<BsonDocument>(store.CollectionName, identity.AutoId);
                var operationId = Guid.NewGuid();
                long inserted = 0;
                foreach (var batch in documents.Chunk(128))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    inserted += collection.InsertBulk(batch, 128);
                }

                for (var i = 0; i < items.Count; i++)
                {
                    items[i].Id = identity.FromBson(documents[i]["_id"]);
                    options.Session?.CaptureMutation(database, items[i], null, documents[i], store.CollectionName, "Insert", "BulkInsert", operationId, items.Count);
                }

                return inserted;
            }, true, cancellationToken).ConfigureAwait(false);
            return Result<long>.Success(count);
        }
        catch (OperationCanceledException) { throw; }
        catch (LiteDbProviderException exception) { return Result<long>.Failure().WithErrors(exception.Errors).WithMessages(exception.Messages); }
        catch (Exception exception) when (!exception.IsTransientException()) { return Result<long>.Failure(new ExceptionError(exception)); }
    }
}
