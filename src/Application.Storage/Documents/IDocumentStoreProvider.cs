// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the Result-native backend contract implemented by document-store providers.
/// </summary>
/// <example>
/// <code>
/// var result = await provider.GetResultAsync&lt;Person&gt;(
///     new DocumentKey("people", "42"),
///     cancellationToken);
/// </code>
/// </example>
public interface IDocumentStoreProvider
{
    /// <summary>
    /// Gets the provider query capabilities.
    /// </summary>
    DocumentStoreProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Retrieves one document by exact key.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="documentKey">The exact partition and row key of the document to retrieve.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the document, or a failure when the document was not found or the provider failed.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.GetResultAsync&lt;Person&gt;(
    ///     new DocumentKey("people", "42"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<T>> GetResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves one bounded page of document payloads.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="query">The query describing key filters, page size, continuation token, and full-scan intent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing one page of documents and an optional continuation token.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.FindPageResultAsync&lt;Person&gt;(
    ///     DocumentQueries.Query()
    ///         .ForKey("people", "DE-")
    ///         .WithRowKeyPrefix()
    ///         .Take(50)
    ///         .Build(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentPage<T>>> FindPageResultAsync<T>(DocumentQuery query, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Retrieves one bounded page of document keys only.
    /// </summary>
    /// <typeparam name="T">The document payload type whose keys should be listed.</typeparam>
    /// <param name="query">The query describing key filters, page size, continuation token, and full-scan intent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing one page of document keys and an optional continuation token.</returns>
    /// <example>
    /// <code>
    /// var keys = await provider.ListPageResultAsync&lt;Person&gt;(
    ///     DocumentQueries.Query()
    ///         .ForKey("people", "DE-")
    ///         .WithRowKeyPrefix()
    ///         .Take(100)
    ///         .Build(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentKeyPage>> ListPageResultAsync<T>(DocumentQuery query, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Counts documents matching a query.
    /// </summary>
    /// <typeparam name="T">The document payload type whose documents should be counted.</typeparam>
    /// <param name="query">The count query describing key filters and full-scan intent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the number of matching documents.</returns>
    /// <example>
    /// <code>
    /// var count = await provider.CountResultAsync&lt;Person&gt;(
    ///     DocumentQueries.Count()
    ///         .ForKey("people", "DE-")
    ///         .WithRowKeyPrefix()
    ///         .Build(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<long>> CountResultAsync<T>(DocumentCountQuery query, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Checks exact-key existence.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="documentKey">The exact partition and row key to check.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing <c>true</c> when the document exists; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var exists = await provider.ExistsResultAsync&lt;Person&gt;(
    ///     new DocumentKey("people", "42"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Inserts or updates one document.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="documentKey">The exact partition and row key to store the document under.</param>
    /// <param name="entity">The document payload to insert or replace.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether the upsert completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.UpsertResultAsync(
    ///     new DocumentKey("people", "42"),
    ///     person,
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> UpsertResultAsync<T>(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Inserts or updates multiple documents.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="entities">The document keys and payloads to insert or replace.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether all upserts completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.UpsertResultAsync(
    ///     people.Select(person => (new DocumentKey("people", person.Id), person)),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> UpsertResultAsync<T>(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Deletes a document by exact key.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <param name="documentKey">The exact partition and row key of the document to delete.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether the delete completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await provider.DeleteResultAsync&lt;Person&gt;(
    ///     new DocumentKey("people", "42"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> DeleteResultAsync<T>(DocumentKey documentKey, CancellationToken cancellationToken = default)
        where T : class, new();
}
