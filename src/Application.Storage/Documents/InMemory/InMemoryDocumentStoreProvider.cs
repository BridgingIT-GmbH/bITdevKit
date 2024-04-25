// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class InMemoryDocumentStoreProvider : IDocumentStoreProvider
{
    public InMemoryDocumentStoreProvider( // TODO: add Options ctor
        ILoggerFactory loggerFactory,
        InMemoryDocumentStoreContext context = null)
    {
        this.Logger = loggerFactory?.CreateLogger<InMemoryDocumentStoreProvider>() ?? NullLoggerFactory.Instance.CreateLogger<InMemoryDocumentStoreProvider>();
        this.Context = context ?? new InMemoryDocumentStoreContext();
    }

    protected ILogger<InMemoryDocumentStoreProvider> Logger { get; }

    protected InMemoryDocumentStoreContext Context { get; }

    /// <summary>
    /// Retrieves entities of type T from document store asynchronously
    /// </summary>
    public Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(this.Context.Find<T>());
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
    public Task<IEnumerable<T>> FindAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.FromResult(
            this.Context.Find<T>(new(documentKey.PartitionKey, documentKey.RowKey), filter));
    }

    public Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(this.Context.List<T>());
    }

    public async Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return await this.ListAsync<T>(documentKey, DocumentKeyFilter.FullMatch, cancellationToken);
    }

    public Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));

        return Task.FromResult(this.Context.List<T>(documentKey, filter));
    }

    /// <summary>
    /// Counts the number of entities of type T in the document store
    /// </summary>
    public Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Task.FromResult(
            this.Context.Find<T>().LongCount());
    }

    /// <summary>
    /// Checks if an entity of type T with given whole partitionKey and whole rowKey exists in the document store
    /// </summary>
    public Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.FromResult(
            this.Context.Find<T>(
                new(documentKey.PartitionKey, documentKey.RowKey)).Any());
    }

    /// <summary>
    /// Inserts or updates an entity of type T in the document store
    /// </summary>
    public Task UpsertAsync<T>(
        DocumentKey documentKey,
        T entity,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.Run(() =>
          this.Context.AddOrUpdate(
              entity.Clone(),
              new(documentKey.PartitionKey, documentKey.RowKey)), cancellationToken);
    }

    /// <summary>
    /// Inserts or updates multiple entities of type T in the document store
    /// </summary>
    public async Task UpsertAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new()
    {
        EnsureArg.IsNotNull(entities, nameof(entities));

        foreach (var (documentKey, entity) in entities)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await this.UpsertAsync(documentKey, entity, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes an entity of type T with the specified whole partitionKey and whole rowKey from the document store
    /// </summary>
    public Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
where T : class, new()
    {
        EnsureArg.IsNotNullOrEmpty(documentKey.PartitionKey, nameof(documentKey.PartitionKey));
        EnsureArg.IsNotNullOrEmpty(documentKey.RowKey, nameof(documentKey.RowKey));

        return Task.Run(() =>
            this.Context.Delete<T>(
                new(documentKey.PartitionKey, documentKey.RowKey)), cancellationToken);
    }
}