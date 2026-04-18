// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the backend contract implemented by document-store providers.
/// </summary>
/// <remarks>
/// Application code usually depends on <see cref="IDocumentStoreClient{T}" />. Providers implement this lower-level contract
/// so the default client and its behaviors can delegate storage operations consistently across backends.
/// </remarks>
public interface IDocumentStoreProvider
{
    /// <summary>
    /// Retrieves all documents of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching documents.</returns>
    Task<IEnumerable<T>> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves documents by exact <paramref name="documentKey" />.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="documentKey">The exact partition and row key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching documents.</returns>
    Task<IEnumerable<T>> FindAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves documents using the supplied key and filter semantics.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="documentKey">The key values that seed the query.</param>
    /// <param name="filter">The filter semantics to apply to the supplied key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching documents.</returns>
    Task<IEnumerable<T>> FindAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Lists all document keys for documents of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching document keys.</returns>
    Task<IEnumerable<DocumentKey>> ListAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Lists document keys for an exact <paramref name="documentKey" />.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="documentKey">The exact partition and row key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching document keys.</returns>
    Task<IEnumerable<DocumentKey>> ListAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Lists document keys using the supplied key and filter semantics.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="documentKey">The key values that seed the query.</param>
    /// <param name="filter">The filter semantics to apply to the supplied key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching document keys.</returns>
    Task<IEnumerable<DocumentKey>> ListAsync<T>(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Counts the number of stored documents of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The document type to count.</typeparam>
    /// <param name="cancellationToken">A token to observe while waiting for the count to complete.</param>
    /// <returns>The number of stored documents.</returns>
    Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Checks whether a document exists for the supplied exact key.
    /// </summary>
    /// <typeparam name="T">The document type to query.</typeparam>
    /// <param name="documentKey">The exact partition and row key to check.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lookup to complete.</param>
    /// <returns><see langword="true" /> when a document exists for the supplied key; otherwise <see langword="false" />.</returns>
    Task<bool> ExistsAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Inserts or updates a single document.
    /// </summary>
    /// <typeparam name="T">The document type to persist.</typeparam>
    /// <param name="documentKey">The exact partition and row key of the document.</param>
    /// <param name="entity">The document payload to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the write to complete.</param>
    Task UpsertAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Inserts or updates a batch of documents.
    /// </summary>
    /// <typeparam name="T">The document type to persist.</typeparam>
    /// <param name="entities">The keys and payloads to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the write to complete.</param>
    Task UpsertAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Deletes the document stored under the supplied exact key.
    /// </summary>
    /// <typeparam name="T">The document type to delete.</typeparam>
    /// <param name="documentKey">The exact partition and row key of the document to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the delete to complete.</param>
    Task DeleteAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();
}
