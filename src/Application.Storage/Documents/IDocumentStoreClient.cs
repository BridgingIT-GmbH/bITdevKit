// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// A generic client interface which provides methods to interact with a <see cref="IDocumentStoreProvider"/> of type T.
/// </summary>
public interface IDocumentStoreClient<T>
     where T : class, new()
{
    /// <summary>
    /// Retrieves entities of type T from document store asynchronously
    /// </summary>
    Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document keys of type T
    /// </summary>
    Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document keys of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document keys of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of entities of type T in the document store
    /// </summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an entity of type T with given partitionKey and rowKey exists in the document store
    /// </summary>
    Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates an entity of type T in the document store
    /// </summary>
    Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates multiple entities of type T in the document store
    /// </summary>
    Task UpsertAsync(IEnumerable<(DocumentKey DocumentKey, T Entity)> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity of type T with the specified partitionKey and rowKey from the document store
    /// </summary>
    Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);
}