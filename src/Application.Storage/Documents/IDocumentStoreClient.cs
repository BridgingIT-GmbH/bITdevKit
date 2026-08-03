// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the public, metadata-aware API for storing and querying typed documents.
/// </summary>
/// <typeparam name="T">
/// The application document type. Values are serialized by the configured client before they cross the provider boundary.
/// </typeparam>
/// <remarks>
/// Implementations expose provider-neutral metadata such as entity tags, content hashes, timestamps, expiration, and custom
/// properties. Documents whose expiration is due at an operation's visibility cutoff are excluded from reads and queries,
/// although their physical records can remain until retention processing removes them.
/// </remarks>
/// <example>
/// <code>
/// var key = new DocumentKey("customers", "42");
/// var result = await documents.GetAsync(key, cancellationToken);
/// if (result.IsSuccess)
/// {
///     var customer = result.Value.Value;
///     var etag = result.Value.ETag;
/// }
/// </code>
/// </example>
public interface IDocumentStoreClient<T> where T : class, new()
{
    /// <summary>
    /// Gets one document and its provider-neutral metadata by exact key.
    /// </summary>
    /// <param name="key">The exact partition and row key identifying the document.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing the typed document and metadata when it exists and is visible. A missing or expired document
    /// produces a not-found failure.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await documents.GetAsync(
    ///     new DocumentKey("customers", "42"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one bounded, deterministically ordered page of visible documents and their metadata.
    /// </summary>
    /// <param name="query">
    /// The bounded query, including key filters, page size, optional continuation token, and explicit full-scan approval.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing one page and an opaque continuation token when more matching documents are available.
    /// Continuation pages retain the visibility cutoff established by the first page.
    /// </returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("customers", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Take(100)
    ///     .Build();
    /// var page = await documents.FindPageAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one bounded, deterministically ordered page of visible document keys without returning document payloads.
    /// </summary>
    /// <param name="query">
    /// The bounded query, including key filters, page size, optional continuation token, and explicit full-scan approval.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing one key-only page and an opaque continuation token when more matching keys are available.
    /// </returns>
    /// <remarks>
    /// Providers that support key-only projection can execute this operation without materializing serialized payloads.
    /// Use this operation for bounded maintenance workflows that do not need document values.
    /// </remarks>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("customers", string.Empty)
    ///     .WithRowKeyPrefix()
    ///     .Take(250)
    ///     .Build();
    /// var page = await documents.ListPageAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts documents matching a query that are visible at the operation cutoff.
    /// </summary>
    /// <param name="query">
    /// The count query containing key filters and explicit full-scan approval when no key constraint is supplied.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A result containing the number of matching, non-expired documents.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Count()
    ///     .ForKey("customers", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Build();
    /// var count = await documents.CountAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a document exists and is visible by exact key.
    /// </summary>
    /// <param name="key">The exact partition and row key identifying the document.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing <see langword="true" /> when the document exists and has not expired at the operation cutoff;
    /// otherwise <see langword="false" />.
    /// </returns>
    /// <example>
    /// <code>
    /// var exists = await documents.ExistsAsync(
    ///     new DocumentKey("customers", "42"),
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces one document after serialization, transformation, size validation, and integrity hashing.
    /// </summary>
    /// <param name="key">The partition and row key under which the document is stored.</param>
    /// <param name="value">The non-null document value to serialize and store.</param>
    /// <param name="options">
    /// Optional concurrency, create-only, expected-hash, property, and expiration settings. When omitted, an existing
    /// document is replaced while its current expiration and properties are preserved.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing the stored metadata. Failed create-only or ETag conditions produce a conflict failure;
    /// expected-hash mismatches produce an integrity failure.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await documents.UpsertAsync(
    ///     new DocumentKey("customers", customer.Id),
    ///     customer,
    ///     new DocumentWriteOptions
    ///     {
    ///         IfMatchETag = current.ETag,
    ///         Expiration = ExpirationChange.After(TimeSpan.FromDays(30))
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces an ordered collection of documents and reports explicit partial completion.
    /// </summary>
    /// <param name="writes">
    /// The writes to validate completely and process in input order. Each item can carry independent concurrency,
    /// integrity, property, and expiration options.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing metadata for successful writes in input order and the first failed key when processing stops.
    /// The complete collection is not guaranteed to be atomic across provider transaction boundaries.
    /// </returns>
    /// <remarks>
    /// All input is materialized and validated before the first provider write. Once processing begins, a provider failure
    /// can leave earlier items committed; callers must inspect the returned <see cref="DocumentBatchResult{T}" />.
    /// </remarks>
    /// <example>
    /// <code>
    /// var writes = customers
    ///     .Select(customer =&gt; new DocumentWrite&lt;Customer&gt;(
    ///         new DocumentKey("customers", customer.Id),
    ///         customer))
    ///     .ToArray();
    /// var result = await documents.UpsertManyAsync(writes, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically updates custom properties and expiration without replacing or reserializing the document payload.
    /// </summary>
    /// <param name="update">
    /// The metadata mutation. A null property bag preserves current properties; a non-null bag replaces them.
    /// Expiration defaults to preserve and an optional ETag can guard the update.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing updated metadata. The logical content hash remains unchanged. A failed ETag condition produces
    /// a conflict failure.
    /// </returns>
    /// <example>
    /// <code>
    /// var properties = new PropertyBag();
    /// properties.Set("reviewed", true);
    /// var result = await documents.UpdatePropertiesAsync(
    ///     new DocumentPropertiesUpdate(new DocumentKey("customers", "42"))
    ///     {
    ///         IfMatchETag = current.ETag,
    ///         Properties = properties,
    ///         Expiration = ExpirationChange.Clear
    ///     },
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one physical document by exact key, optionally guarded by its current ETag.
    /// </summary>
    /// <param name="key">The exact partition and row key identifying the document.</param>
    /// <param name="options">Optional conditional-delete settings.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A successful result when the document is deleted or already absent. A failed ETag condition produces a conflict
    /// failure. Expired physical records can still be deleted explicitly.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await documents.DeleteAsync(
    ///     current.Key,
    ///     new DocumentDeleteOptions { IfMatchETag = current.ETag },
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an ordered collection of physical documents and reports explicit partial completion.
    /// </summary>
    /// <param name="deletes">
    /// The exact keys and optional per-item ETag conditions to validate completely and process in input order.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A result containing successfully processed keys in input order and the first failed key when processing stops.
    /// The complete collection is not guaranteed to be atomic across provider transaction boundaries.
    /// </returns>
    /// <remarks>
    /// All input is materialized and validated before the first provider delete. Deleting an already absent item is
    /// idempotent, while an ETag mismatch is reported as a conflict and can leave earlier deletes committed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var deletes = entries
    ///     .Select(entry =&gt; new DocumentDelete(
    ///         entry.Key,
    ///         new DocumentDeleteOptions { IfMatchETag = entry.ETag }))
    ///     .ToArray();
    /// var result = await documents.DeleteManyAsync(deletes, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default);
}
