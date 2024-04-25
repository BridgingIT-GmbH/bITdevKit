// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Generic;

public interface IDocumentStoreProvider
{
    /// <summary>
    /// Retrieves entities of type T from document store asynchronously
    /// </summary>
    Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves entities of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<T>> FindAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves entities of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<T>> FindAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves document keys of type T
    /// </summary>
    Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
    where T : class, new();

    /// <summary>
    /// Retrieves document keys of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves document keys of type T filtered by the partitionKey and rowKey
    /// </summary>
    Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, DocumentKeyFilter filter, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Counts the number of entities of type T in the document store
    /// </summary>
    Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
    where T : class, new();

    /// <summary>
    /// Checks if an entity of type T with given partitionKey and rowKey exists in the document store
    /// </summary>
    Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Inserts or updates an entity of type T in the document store
    /// </summary>
    Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Inserts or updates multiple entities of type T in the document store
    /// </summary>
    Task UpsertAsync<T>(IEnumerable<(DocumentKey DocumentKey, T Entity)> entities, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Deletes an entity of type T with the specified partitionKey and rowKey from the document store
    /// </summary>
    Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();
}