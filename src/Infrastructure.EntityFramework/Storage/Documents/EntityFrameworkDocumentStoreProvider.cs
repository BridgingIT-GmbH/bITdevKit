// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;

public class EntityFrameworkDocumentStoreProvider<TContext> : IDocumentStoreProvider
    where TContext : DbContext, IDocumentStoreContext
{
    private readonly TContext context;
    private readonly ISerializer serializer;

    public EntityFrameworkDocumentStoreProvider( // TODO: add ctor which accepts Options
        TContext context,
        ISerializer serializer = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    /// <summary>
    ///     Retrieves entities of type T from document store asynchronously
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();

        return (await this.context.StorageDocuments.Where(e => e.Type == type)
            .AsNoTracking()
            .ToListAsync(cancellationToken)).ConvertAll(e => this.serializer.Deserialize<T>(e.Content));
    }

    /// <summary>
    ///     Retrieves entities of type T filtered by the whole partitionKey and whole rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.FindAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    /// <summary>
    ///     Searches for entities of type T by the whole partitionKey and startswith rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        if (filter == DocumentKeyFilter.FullMatch)
        {
            return (await this.context.StorageDocuments.AsNoTracking()
                .Where(e => e.Type == type &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey == documentKey.RowKey)
                .ToListAsync(cancellationToken)).ConvertAll(e => this.serializer.Deserialize<T>(e.Content));
        }

        if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            return (await this.context.StorageDocuments.AsNoTracking()
                .Where(e => e.Type == type &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey.StartsWith(documentKey.RowKey))
                .ToListAsync(cancellationToken)).ConvertAll(e => this.serializer.Deserialize<T>(e.Content));
        }

        if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            return (await this.context.StorageDocuments.AsNoTracking()
                .Where(e => e.Type == type &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey.EndsWith(documentKey.RowKey))
                .ToListAsync(cancellationToken)).ConvertAll(e => this.serializer.Deserialize<T>(e.Content));
        }

        return [];
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();

        return await this.context.StorageDocuments.AsNoTracking()
            .Where(e => e.Type == type)
            .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        //EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        if (filter == DocumentKeyFilter.FullMatch)
        {
            return await this.context.StorageDocuments.AsNoTracking()
                .Where(e => e.Type == type &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey == documentKey.RowKey)
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                .ToListAsync(cancellationToken);
        }

        if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            return await this.context.StorageDocuments.AsNoTracking()
                .Where(e => e.Type == type &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey.StartsWith(documentKey.RowKey))
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                .ToListAsync(cancellationToken);
        }

        if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            return await this.context.StorageDocuments.AsNoTracking()
                .Where(e => e.Type == type &&
                    e.PartitionKey == documentKey.PartitionKey &&
                    e.RowKey.EndsWith(documentKey.RowKey))
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey))
                .ToListAsync(cancellationToken);
        }

        return [];
    }

    /// <summary>
    ///     Counts the number of entities of type T in the document store
    /// </summary>
    public async Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();

        return (await this.context.StorageDocuments.AsNoTracking()
            .Where(e => e.Type == type)
            .ToListAsync(cancellationToken)).Count;
    }

    /// <summary>
    ///     Checks if an entity of type T with given whole partitionKey and whole rowKey exists in the document store
    /// </summary>
    public async Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();

        return (await this.context.StorageDocuments.AsNoTracking()
            .Where(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
            .ToListAsync(cancellationToken)).SafeAny();
    }

    /// <summary>
    ///     Inserts or updates an entity of type T in the document store
    /// </summary>
    public async Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        var documentEntity = await this.context.StorageDocuments.FirstOrDefaultAsync(e =>
                e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey,
            cancellationToken);
        if (documentEntity is null)
        {
            documentEntity = new StorageDocument
            {
                Type = type,
                PartitionKey = documentKey.PartitionKey,
                RowKey = documentKey.RowKey
            };

            this.context.StorageDocuments.Add(documentEntity);
        }
        else
        {
            // TODO: check if ContentHash was changed, don't save if they are the same (NOT_UPDATED) > return
            documentEntity.UpdatedDate = DateTime.UtcNow;
        }

        documentEntity.Content = this.serializer.SerializeToString(entity);
        documentEntity.ContentHash = HashHelper.Compute(entity);

        await this.context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entities, nameof(entities));

        if (entities.SafeAny())
        {
            var type = this.GetTypeName<T>();
            foreach (var (documentKey, entity) in entities.SafeNull())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var documentEntity = await this.context.StorageDocuments.FirstOrDefaultAsync(e =>
                        e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey,
                    cancellationToken);
                if (documentEntity is null)
                {
                    documentEntity = new StorageDocument
                    {
                        Type = type,
                        PartitionKey = documentKey.PartitionKey,
                        RowKey = documentKey.RowKey
                    };

                    this.context.StorageDocuments.Add(documentEntity);
                }
                else
                {
                    // TODO: check if ContentHash was changed, don't save if they are the same (NOT_UPDATED) > continue
                    documentEntity.UpdatedDate = DateTime.UtcNow;
                }

                documentEntity.Content = this.serializer.SerializeToString(entity);
                documentEntity.ContentHash = HashHelper.Compute(entity);
            }

            await this.context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Deletes an entity of type T with the specified whole partitionKey and whole rowKey from the document store
    /// </summary>
    public async Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        var documentEntities = await this.context.StorageDocuments
            .Where(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
            .ToListAsync(cancellationToken);
        if (documentEntities.SafeAny())
        {
            foreach (var documentEntity in documentEntities)
            {
                this.context.StorageDocuments.Remove(documentEntity);
            }

            await this.context.SaveChangesAsync(cancellationToken);
        }
    }

    private string GetTypeName<T>()
    {
        return typeof(T).FullName.ToLowerInvariant().TruncateLeft(1024);
    }
}