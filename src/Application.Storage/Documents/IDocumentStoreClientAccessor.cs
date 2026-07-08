// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides non-generic dashboard access to one selected typed document-store client.
/// </summary>
/// <example>
/// <code>
/// var json = await accessor.GetJsonResultAsync(new DocumentKey("people", "person-1"), ct);
/// </code>
/// </example>
public interface IDocumentStoreClientAccessor
{
    /// <summary>
    /// Gets the selected client descriptor.
    /// </summary>
    /// <example>
    /// <code>
    /// var descriptor = accessor.Descriptor;
    /// </code>
    /// </example>
    DocumentStoreClientDescriptor Descriptor { get; }

    /// <summary>
    /// Lists one page of document keys for the selected typed client.
    /// </summary>
    /// <param name="query">The document query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing the key page.</returns>
    /// <example>
    /// <code>
    /// var result = await accessor.ListPageResultAsync(DocumentQueries.Query().Take(50).Build(), ct);
    /// </code>
    /// </example>
    Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts documents for the selected typed client.
    /// </summary>
    /// <param name="query">The count query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing the matching count.</returns>
    /// <example>
    /// <code>
    /// var count = await accessor.CountResultAsync(DocumentQueries.Count().AllowFullScan().Build(), ct);
    /// </code>
    /// </example>
    Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one exact document and renders it as serialized text.
    /// </summary>
    /// <param name="documentKey">The exact document key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing serialized document content.</returns>
    /// <example>
    /// <code>
    /// var result = await accessor.GetJsonResultAsync(new DocumentKey("people", "person-1"), ct);
    /// </code>
    /// </example>
    Task<Result<string>> GetJsonResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses serialized text into the selected document type and upserts it through the typed client.
    /// </summary>
    /// <param name="documentKey">The exact document key.</param>
    /// <param name="content">The serialized document content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result indicating whether the upsert succeeded.</returns>
    /// <example>
    /// <code>
    /// var result = await accessor.UpsertJsonResultAsync(new DocumentKey("people", "person-1"), json, ct);
    /// </code>
    /// </example>
    Task<Result> UpsertJsonResultAsync(DocumentKey documentKey, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one exact document through the selected typed client.
    /// </summary>
    /// <param name="documentKey">The exact document key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result indicating whether the delete succeeded.</returns>
    /// <example>
    /// <code>
    /// var result = await accessor.DeleteResultAsync(new DocumentKey("people", "person-1"), ct);
    /// </code>
    /// </example>
    Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default);
}
