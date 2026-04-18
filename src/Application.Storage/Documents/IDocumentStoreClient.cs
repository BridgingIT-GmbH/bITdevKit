// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the public API used by application code to query and mutate typed documents.
/// </summary>
/// <typeparam name="T">The document type handled by the client.</typeparam>
/// <example>
/// <code>
/// public sealed class PeopleService(IDocumentStoreClient&lt;Person&gt; documents)
/// {
///     public Task SaveAsync(Person person, CancellationToken ct) =>
///         documents.UpsertAsync(new DocumentKey(person.Country, person.Id.ToString()), person, ct);
///
///     public Task&lt;IEnumerable&lt;Person&gt;&gt; FindByPrefixAsync(string country, string prefix, CancellationToken ct) =>
///         documents.FindAsync(new DocumentKey(country, prefix), DocumentKeyFilter.RowKeyPrefixMatch, ct);
/// }
/// </code>
/// </example>
public interface IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Retrieves all documents of type <typeparamref name="T" />.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching documents.</returns>
    Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents by exact <paramref name="documentKey" />.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching documents.</returns>
    Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents using the supplied key and filter semantics.
    /// </summary>
    /// <param name="documentKey">The key values that seed the query.</param>
    /// <param name="filter">The filter semantics to apply to the supplied key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching documents.</returns>
    Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all document keys for documents of type <typeparamref name="T" />.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The document keys for the requested type.</returns>
    Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists document keys for an exact <paramref name="documentKey" />.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key to query.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching document keys.</returns>
    Task<IEnumerable<DocumentKey>> ListAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists document keys using the supplied key and filter semantics.
    /// </summary>
    /// <param name="documentKey">The key values that seed the query.</param>
    /// <param name="filter">The filter semantics to apply to the supplied key.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query to complete.</param>
    /// <returns>The matching document keys.</returns>
    Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of stored documents of type <typeparamref name="T" />.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the count to complete.</param>
    /// <returns>The number of stored documents.</returns>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a document exists for the supplied exact key.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key to check.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the lookup to complete.</param>
    /// <returns><see langword="true" /> when a document exists for the supplied key; otherwise <see langword="false" />.</returns>
    Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a single document.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key of the document.</param>
    /// <param name="entity">The document payload to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the write to complete.</param>
    Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a batch of documents.
    /// </summary>
    /// <param name="entities">The keys and payloads to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the write to complete.</param>
    Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the document stored under the supplied exact key.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key of the document to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the delete to complete.</param>
    Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);
}
