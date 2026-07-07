// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the public Result-native API used by application code to query and mutate typed documents.
/// </summary>
/// <typeparam name="T">The document type handled by the client.</typeparam>
/// <example>
/// <code>
/// var query = DocumentQueries.Query()
///     .ForKey("people", "DE-")
///     .WithRowKeyPrefix()
///     .Take(100)
///     .Build();
///
/// var page = await documents.FindPageResultAsync(query, cancellationToken);
/// </code>
/// </example>
public interface IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Retrieves one document by exact key.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key of the document to retrieve.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the document, or a failure when the document was not found or the provider failed.</returns>
    /// <example>
    /// <code>
    /// var result = await documents.GetResultAsync(new DocumentKey("people", "42"), cancellationToken);
    /// if (result.IsSuccess)
    /// {
    ///     var person = result.Value;
    /// }
    /// </code>
    /// </example>
    Task<Result<T>> GetResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one bounded page of document payloads.
    /// </summary>
    /// <param name="query">The query describing key filters, page size, continuation token, and full-scan intent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing one page of documents and an optional continuation token.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Take(50)
    ///     .Build();
    ///
    /// var page = await documents.FindPageResultAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentPage<T>>> FindPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one bounded page of document keys only.
    /// </summary>
    /// <param name="query">The query describing key filters, page size, continuation token, and full-scan intent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing one page of document keys and an optional continuation token.</returns>
    /// <example>
    /// <code>
    /// var keys = await documents.ListPageResultAsync(
    ///     DocumentQueries.Query()
    ///         .ForKey("people", "DE-")
    ///         .WithRowKeyPrefix()
    ///         .Take(100)
    ///         .Build(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts documents matching a query.
    /// </summary>
    /// <param name="query">The count query describing key filters and full-scan intent.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the number of matching documents.</returns>
    /// <example>
    /// <code>
    /// var count = await documents.CountResultAsync(
    ///     DocumentQueries.Count()
    ///         .ForKey("people", "DE-")
    ///         .WithRowKeyPrefix()
    ///         .Build(),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks exact-key existence.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key to check.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing <c>true</c> when the document exists; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// var exists = await documents.ExistsResultAsync(new DocumentKey("people", "42"), cancellationToken);
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates one document.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key to store the document under.</param>
    /// <param name="entity">The document payload to insert or replace.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether the upsert completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await documents.UpsertResultAsync(
    ///     new DocumentKey("people", "42"),
    ///     person,
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> UpsertResultAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates multiple documents.
    /// </summary>
    /// <param name="entities">The document keys and payloads to insert or replace.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether all upserts completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await documents.UpsertResultAsync(
    ///     people.Select(person => (new DocumentKey("people", person.Id), person)),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document by exact key.
    /// </summary>
    /// <param name="documentKey">The exact partition and row key of the document to delete.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result indicating whether the delete completed successfully.</returns>
    /// <example>
    /// <code>
    /// var result = await documents.DeleteResultAsync(new DocumentKey("people", "42"), cancellationToken);
    /// </code>
    /// </example>
    Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);
}
