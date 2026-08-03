// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides non-generic, JSON-oriented access to one selected typed document client for operational presentation surfaces.
/// </summary>
/// <remarks>
/// The dashboard uses this adapter after selecting a <see cref="DocumentStoreClientDescriptor" />. Implementations delegate
/// to the same container-owned typed client used by application code, preserving behaviors, validation, concurrency,
/// expiration, and provider lifetime. It is not a second persistence API for normal application use.
/// </remarks>
/// <example>
/// <code>
/// var entry = await accessor.GetEntryJsonAsync(key, cancellationToken);
/// </code>
/// </example>
public interface IDocumentStoreClientAccessor
{
    /// <summary>
    /// Gets immutable registration identity, provider, lifetime, type, and capability metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// Console.WriteLine(accessor.Descriptor.Name);
    /// </code>
    /// </example>
    DocumentStoreClientDescriptor Descriptor { get; }

    /// <summary>
    /// Gets whether this client registration opted into Storage Permalink tracking.
    /// </summary>
    /// <example>
    /// <code>
    /// if (accessor.PermalinksEnabled) { /* expose permalink actions */ }
    /// </code>
    /// </example>
    bool PermalinksEnabled { get; }

    /// <summary>
    /// Lists one bounded page of logically visible document keys.
    /// </summary>
    /// <param name="query">
    /// The bounded query, filters, continuation token, and explicit full-scan approval.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result containing a key-only page and optional continuation token.
    /// </returns>
    /// <example>
    /// <code>
    /// var page = await accessor.ListPageAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one bounded page of dashboard-safe JSON entries with metadata and serialized sizes.
    /// </summary>
    /// <param name="query">
    /// The bounded query, filters, continuation token, and explicit full-scan approval.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result containing JSON entries and an optional continuation token.
    /// </returns>
    /// <example>
    /// <code>
    /// var page = await accessor.FindJsonPageAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentJsonPage>> FindJsonPageAsync(DocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts documents matching a validated query that are logically visible at the operation cutoff.
    /// </summary>
    /// <param name="query">
    /// The count query and explicit full-scan approval.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result containing the matching document count.
    /// </returns>
    /// <example>
    /// <code>
    /// var count = await accessor.CountAsync(query, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an exact-key document exists and is logically visible.
    /// </summary>
    /// <param name="key">
    /// The exact partition and row key.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result containing true only when a non-expired document exists.
    /// </returns>
    /// <example>
    /// <code>
    /// var exists = await accessor.ExistsAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one typed document serialized into dashboard-safe JSON.
    /// </summary>
    /// <param name="key">
    /// The exact partition and row key.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result containing JSON content, or a not-found failure for a missing or expired document.
    /// </returns>
    /// <example>
    /// <code>
    /// var json = await accessor.GetJsonAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<string>> GetJsonAsync(DocumentKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one dashboard-safe JSON payload together with provider-neutral metadata.
    /// </summary>
    /// <param name="key">
    /// The exact partition and row key.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result containing JSON and metadata used for display and conditional mutation.
    /// </returns>
    /// <example>
    /// <code>
    /// var entry = await accessor.GetEntryJsonAsync(key, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentJsonEntry>> GetEntryJsonAsync(DocumentKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes JSON into the selected client type and writes it through the normal typed client pipeline.
    /// </summary>
    /// <param name="key">
    /// The partition and row key under which the document is written.
    /// </param>
    /// <param name="content">
    /// The JSON payload to deserialize.
    /// </param>
    /// <param name="options">
    /// Optional ETag, create-only, integrity, property, and expiration settings.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A result indicating whether deserialization, validation, and persistence succeeded.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await accessor.UpsertJsonAsync(key, json, options, cancellationToken);
    /// </code>
    /// </example>
    Task<Result> UpsertJsonAsync(DocumentKey key, string content, DocumentWriteOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one physical document, optionally guarded by the ETag displayed by the dashboard.
    /// </summary>
    /// <param name="key">
    /// The exact partition and row key.
    /// </param>
    /// <param name="options">
    /// Optional conditional-delete settings.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A successful result for deletion or an already absent record; ETag mismatch returns a conflict.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await accessor.DeleteAsync(key, options, cancellationToken);
    /// </code>
    /// </example>
    Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the document and gets or creates its stable permalink when this registration opted into permalink behavior.
    /// </summary>
    /// <param name="key">
    /// The exact partition and row key.
    /// </param>
    /// <param name="options">
    /// Optional initial permalink expiration.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel verification and registry access.
    /// </param>
    /// <returns>
    /// The stable permalink entry, or a not-enabled result when the client did not opt in.
    /// </returns>
    /// <example>
    /// <code>
    /// var link = await accessor.GetPermalinkAsync(key, options, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(DocumentKey key, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains a dashboard-safe JSON representation and the metadata snapshot used for conditional actions.
/// </summary>
/// <example>
/// <code>
/// var etag = entry.Info.ETag;
/// </code>
/// </example>
public sealed record DocumentJsonEntry
{
    /// <summary>
    /// Gets the serialized JSON payload.
    /// </summary>
    /// <example>
    /// <code>
    /// var json = entry.Content;
    /// </code>
    /// </example>
    public string Content { get; init; }

    /// <summary>
    /// Gets provider-neutral identity, ETag, hash, timestamp, expiration, and property metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = entry.Info.ETag;
    /// </code>
    /// </example>
    public required DocumentInfo Info { get; init; }

    /// <summary>
    /// Gets the UTF-8 byte size of <see cref="Content" />.
    /// </summary>
    /// <example>
    /// <code>
    /// var size = entry.Size;
    /// </code>
    /// </example>
    public long Size { get; init; }
}

/// <summary>
/// Contains one bounded page of dashboard-safe JSON document entries.
/// </summary>
/// <example>
/// <code>
/// var hasMore = page.ContinuationToken is not null;
/// </code>
/// </example>
public sealed record DocumentJsonPage
{
    /// <summary>
    /// Gets entries in provider order.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var entry in page.Items) { }
    /// </code>
    /// </example>
    public IReadOnlyList<DocumentJsonEntry> Items { get; init; } = [];

    /// <summary>
    /// Gets the opaque token for the next page.
    /// </summary>
    /// <example>
    /// <code>
    /// var next = page.ContinuationToken;
    /// </code>
    /// </example>
    public string ContinuationToken { get; init; }
}
