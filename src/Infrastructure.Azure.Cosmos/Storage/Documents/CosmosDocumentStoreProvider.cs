// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Azure;

public class CosmosDocumentStoreProvider : IDocumentStoreProvider
{
    private readonly ICosmosSqlProvider<CosmosStorageDocument> provider;
    private readonly ISerializer serializer;

    public CosmosDocumentStoreProvider( // TODO: add ctor which accepts Options
        ICosmosSqlProvider<CosmosStorageDocument> provider,
        ISerializer serializer = null)
    {
        EnsureArg.IsNotNull(provider, nameof(provider));

        this.provider = provider;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    /// <summary>
    /// Retrieves entities of type T from document store asynchronously
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();
        return (await this.provider
            .ReadItemsAsync(e => e.Type == type, partitionKeyValue: type, cancellationToken: cancellationToken))
            .Select(e => this.serializer.Deserialize<T>(e.Content));
    }

    /// <summary>
    /// Retrieves entities of type T filtered by the whole partitionKey and whole rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.FindAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    /// <summary>
    /// Searches for entities of type T by the whole partitionKey and startswith rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
    where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        if (filter == DocumentKeyFilter.FullMatch)
        {
            return (await this.provider
                .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey, partitionKeyValue: type, cancellationToken: cancellationToken))
                .Select(e => this.serializer.Deserialize<T>(e.Content));
        }
        else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            return (await this.provider
                .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey.StartsWith(documentKey.RowKey), partitionKeyValue: type, cancellationToken: cancellationToken))
                .Select(e => this.serializer.Deserialize<T>(e.Content));
        }
        else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            return (await this.provider
                .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey.EndsWith(documentKey.RowKey), partitionKeyValue: type, cancellationToken: cancellationToken))
                .Select(e => this.serializer.Deserialize<T>(e.Content));
        }

        return [];
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();
        return (await this.provider
            .ReadItemsAsync(e => e.Type == type, partitionKeyValue: type, cancellationToken: cancellationToken))
            .Select(e => new DocumentKey(e.PartitionKey, e.RowKey));
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));

        var type = this.GetTypeName<T>();
        if (filter == DocumentKeyFilter.FullMatch)
        {
            return (await this.provider
                .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey, partitionKeyValue: type, cancellationToken: cancellationToken))
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey));
        }
        else if (filter == DocumentKeyFilter.RowKeyPrefixMatch)
        {
            return (await this.provider
                .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey.StartsWith(documentKey.RowKey), partitionKeyValue: type, cancellationToken: cancellationToken))
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey));
        }
        else if (filter == DocumentKeyFilter.RowKeySuffixMatch)
        {
            return (await this.provider
                .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey.EndsWith(documentKey.RowKey), partitionKeyValue: type, cancellationToken: cancellationToken))
                .Select(e => new DocumentKey(e.PartitionKey, e.RowKey));
        }

        return [];
    }

    /// <summary>
    /// Counts the number of entities of type T in the document store
    /// </summary>
    public async Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var type = this.GetTypeName<T>();

        return (await this.provider
            .ReadItemsAsync(e => e.Type == type, partitionKeyValue: type, cancellationToken: cancellationToken)).Count();
    }

    /// <summary>
    /// Checks if an entity of type T with given whole partitionKey and whole rowKey exists in the document store
    /// </summary>
    public async Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();

        return (await this.provider
            .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey, partitionKeyValue: type, cancellationToken: cancellationToken)).SafeAny();
    }

    /// <summary>
    /// Inserts or updates an entity of type T in the document store
    /// </summary>
    public async Task UpsertAsync<T>(
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        var document = (await this.provider
            .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey, partitionKeyValue: type, cancellationToken: cancellationToken)).FirstOrDefault();

        if (document is null)
        {
            document = new CosmosStorageDocument
            {
                Id = GuidGenerator.Create($"{documentKey.PartitionKey}-{documentKey.RowKey}").ToString(),
                Type = type,
                PartitionKey = documentKey.PartitionKey,
                RowKey = documentKey.RowKey,
            };
        }
        else
        {
            document.UpdatedDate = DateTime.UtcNow;
        }

        document.Content = this.serializer.SerializeToString(entity);
        document.ContentHash = HashHelper.Compute(entity);

        await this.provider.UpsertItemAsync(document, partitionKeyValue: type, cancellationToken);
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

                var document = (await this.provider
                    .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey, partitionKeyValue: type, cancellationToken: cancellationToken)).FirstOrDefault();
                if (document is null)
                {
                    document = new CosmosStorageDocument
                    {
                        Id = GuidGenerator.Create($"{documentKey.PartitionKey}-{documentKey.RowKey}").ToString(),
                        Type = type,
                        PartitionKey = documentKey.PartitionKey,
                        RowKey = documentKey.RowKey
                    };
                }
                else
                {
                    document.UpdatedDate = DateTime.UtcNow;
                }

                document.Content = this.serializer.SerializeToString(entity);
                document.ContentHash = HashHelper.Compute(entity);

                await this.provider.UpsertItemAsync(document, partitionKeyValue: type, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Deletes an entity of type T with the specified whole partitionKey and whole rowKey from the document store
    /// </summary>
    public async Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        var type = this.GetTypeName<T>();
        var documents = await this.provider
            .ReadItemsAsync(e => e.Type == type && e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey, partitionKeyValue: type, cancellationToken: cancellationToken);

        if (documents.SafeAny())
        {
            foreach (var documentEntity in documents)
            {
                await this.provider.DeleteItemAsync(documentEntity.Id, partitionKeyValue: type, cancellationToken);
            }
        }
    }

    private string GetTypeName<T>() =>
        typeof(T).FullName.ToLowerInvariant().TruncateLeft(1024);
}