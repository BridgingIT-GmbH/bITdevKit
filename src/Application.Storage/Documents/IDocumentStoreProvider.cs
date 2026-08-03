// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the provider-neutral persistence boundary used by Document Storage clients.
/// </summary>
/// <remarks>
/// Providers store serialized and optionally transformed bytes rather than application document types. The outer
/// <see cref="IDocumentStoreClient{T}" /> validates public inputs, serializes values, applies transforms, and verifies
/// integrity. Providers own persistence concerns such as ETags, timestamps, atomic mutation, native paging, logical
/// expiration filtering, resource initialization, and translation of expected backend failures into typed Result errors.
/// </remarks>
/// <example>
/// <code>
/// var cutoff = timeProvider.GetUtcNow();
/// var result = await provider.GetAsync(
///     DocumentTypeIdentity.For&lt;Customer&gt;(),
///     new DocumentKey("customers", "42"),
///     cutoff,
///     cancellationToken);
/// </code>
/// </example>
public interface IDocumentStoreProvider
{
    /// <summary>
    /// Gets the immutable capabilities used for validation, query planning, diagnostics, and dashboard presentation.
    /// </summary>
    /// <example><code>var supportsEtags = provider.Capabilities.SupportsConditionalWrite;</code></example>
    DocumentStoreProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Gets one serialized document by exact key when it is logically visible at the supplied cutoff.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="key">The exact partition and row key identifying the document.</param>
    /// <param name="visibilityCutoff">The UTC instant used to exclude documents whose expiration is due.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>
    /// A result containing copied stored bytes and metadata. Missing and logically expired documents produce a not-found
    /// failure even when an expired physical record remains awaiting retention.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await provider.GetAsync(type, key, visibilityCutoff, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoredDocument>> GetAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one bounded, deterministically ordered page of serialized documents visible at the supplied cutoff.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="query">The validated query containing key filters, page size, and provider-native continuation state.</param>
    /// <param name="visibilityCutoff">
    /// The fixed UTC visibility instant established for the page sequence. Providers must apply the same cutoff to all
    /// continuation pages.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>
    /// A result containing copied serialized records and opaque provider-native continuation state when more results exist.
    /// </returns>
    /// <example><code>var page = await provider.FindPageAsync(type, query, cutoff, cancellationToken);</code></example>
    Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one bounded, deterministically ordered page of visible keys without returning payload bytes.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="query">The validated query containing key filters, page size, and provider-native continuation state.</param>
    /// <param name="visibilityCutoff">The fixed UTC visibility instant for the page sequence.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>A result containing document keys and continuation state when more matching keys exist.</returns>
    /// <remarks>
    /// Providers advertising <see cref="DocumentStoreProviderCapabilities.SupportsKeyOnlyProjection" /> should avoid
    /// reading or materializing payload bytes for this operation.
    /// </remarks>
    /// <example><code>var page = await provider.ListPageAsync(type, query, cutoff, cancellationToken);</code></example>
    Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts matching documents that are logically visible at the supplied cutoff.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="query">The validated count query and key-filter semantics.</param>
    /// <param name="visibilityCutoff">The UTC instant used to exclude due documents.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>A result containing the number of matching visible documents.</returns>
    /// <example><code>var count = await provider.CountAsync(type, query, cutoff, cancellationToken);</code></example>
    Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an exact-key document exists and is logically visible at the supplied cutoff.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="key">The exact partition and row key identifying the document.</param>
    /// <param name="visibilityCutoff">The UTC instant used to exclude due documents.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>
    /// A result containing <see langword="true" /> only when a non-expired document is present; otherwise
    /// <see langword="false" />.
    /// </returns>
    /// <example><code>var exists = await provider.ExistsAsync(type, key, cutoff, cancellationToken);</code></example>
    Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or atomically replaces one validated serialized document.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="write">
    /// The copied stored bytes, logical and stored hashes, transform metadata, properties, resolved expiration, and
    /// concurrency settings supplied by the outer client.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>
    /// A result containing committed metadata. Failed create-only or ETag conditions produce a conflict failure and must
    /// not expose a partially replaced record.
    /// </returns>
    /// <remarks>
    /// When <see cref="StoredDocumentWrite.PreserveExpiration" /> is set, providers preserve the existing expiration on
    /// replacement and store no expiration on insertion. A physical expired record still participates in create-only and
    /// ETag conditions until retention deletes it.
    /// </remarks>
    /// <example><code>var info = await provider.UpsertAsync(type, write, cancellationToken);</code></example>
    Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically updates custom properties and expiration without changing serialized content or its hashes.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="update">The exact key, optional replacement properties, expiration mutation, and ETag condition.</param>
    /// <param name="resolvedExpiresAt">The absolute UTC expiration resolved once by the outer client.</param>
    /// <param name="preserveExpiration">
    /// A value indicating whether the current expiration must be retained instead of applying
    /// <paramref name="resolvedExpiresAt" />.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>A result containing updated metadata while preserving the logical content hash.</returns>
    /// <remarks>
    /// Providers must perform this mutation atomically or compensate a partially applied backend update with a
    /// non-cancelable restore. Failed compensation is reported as a typed partial-update provider error.
    /// </remarks>
    /// <example>
    /// <code>
    /// var info = await provider.UpdatePropertiesAsync(
    ///     type,
    ///     update,
    ///     resolvedExpiresAt,
    ///     preserveExpiration,
    ///     cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one physical document by exact key, optionally guarded by its current ETag.
    /// </summary>
    /// <param name="type">The stable persisted namespace for the application document type.</param>
    /// <param name="key">The exact partition and row key identifying the physical record.</param>
    /// <param name="options">Optional conditional-delete settings.</param>
    /// <param name="cancellationToken">The token used to cancel provider I/O.</param>
    /// <returns>
    /// A successful result when deletion completes or the record is already absent. An ETag mismatch produces a conflict
    /// failure and leaves the current record unchanged.
    /// </returns>
    /// <remarks>Deletion addresses physical records and can therefore remove an expired document hidden from logical reads.</remarks>
    /// <example><code>var result = await provider.DeleteAsync(type, key, options, cancellationToken);</code></example>
    Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default);
}
