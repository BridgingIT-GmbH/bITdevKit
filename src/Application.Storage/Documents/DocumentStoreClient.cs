// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
///     A generic client that implements the <see cref="IDocumentStoreClient
///     
///     
///     
///     
///     
///     <T>
///         "/> interface
///         which provides methods to interact with a <see cref="IDocumentStoreProvider" /> of type T.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DocumentStoreClient<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    public DocumentStoreClient(IDocumentStoreProvider provider)
    {
        EnsureArg.IsNotNull(provider, nameof(provider));

        this.Provider = provider;
    }

    protected IDocumentStoreProvider Provider { get; }

    /// <summary>
    ///     Counts the number of entities of type T in the document store
    /// </summary>
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.Provider.CountAsync<T>(cancellationToken);
    }

    /// <summary>
    ///     Deletes an entity of type T with the specified partitionKey and rowKey from the document store
    /// </summary>
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.Provider.DeleteAsync<T>(documentKey, cancellationToken);
    }

    /// <summary>
    ///     Checks if an entity of type T with given partitionKey and rowKey exists in the document store
    /// </summary>
    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Provider.ExistsAsync<T>(documentKey, cancellationToken);
    }

    /// <summary>
    ///     Retrieves entities of type T from document store asynchronously
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default)
    {
        return await this.Provider.FindAsync<T>(cancellationToken);
    }

    /// <summary>
    ///     Retrieves entities of type T filtered by the partitionKey and rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Provider.FindAsync<T>(documentKey, cancellationToken);
    }

    /// <summary>
    ///     Retrieves entities of type T filtered by the partitionKey and rowKey
    /// </summary>
    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await this.Provider.FindAsync<T>(documentKey, filter, cancellationToken);
    }

    /// <summary>
    ///     Retrieves document keys of type T
    /// </summary>
    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await this.Provider.ListAsync<T>(cancellationToken);
    }

    /// <summary>
    ///     Retrieves document keys of type T filtered by the partitionKey and rowKey
    /// </summary>
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return await this.Provider.ListAsync<T>(documentKey, cancellationToken);
    }

    /// <summary>
    ///     Retrieves document keys of type T filtered by the partitionKey and rowKey
    /// </summary>
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await this.Provider.ListAsync<T>(documentKey, filter, cancellationToken);
    }

    /// <summary>
    ///     Inserts or updates an entity of type T in the document store
    /// </summary>
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        await this.Provider.UpsertAsync(documentKey, entity, cancellationToken);
    }

    /// <summary>
    ///     Inserts or updates multiple entities of type T in the document store
    /// </summary>
    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        await this.Provider.UpsertAsync(entities, cancellationToken);
    }
}