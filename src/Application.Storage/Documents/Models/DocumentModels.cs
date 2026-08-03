// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Identifies the stable persisted namespace used to isolate records belonging to one application document type.
/// </summary>
/// <param name="Value">The non-empty stable type identity persisted and bound into continuation tokens.</param>
/// <remarks>
/// The default identity uses the CLR assembly-qualified name. Applications must treat an identity change as a persisted
/// namespace change; providers do not infer migrations between identities.
/// </remarks>
/// <example><code>var identity = DocumentTypeIdentity.For&lt;Person&gt;();</code></example>
public readonly record struct DocumentTypeIdentity(string Value)
{
    /// <summary>Creates an identity for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <returns>The stable type identity.</returns>
    /// <example><code>var identity = DocumentTypeIdentity.For&lt;Person&gt;();</code></example>
    public static DocumentTypeIdentity For<T>() => For(typeof(T));

    /// <summary>Creates an identity for a CLR type.</summary>
    /// <param name="type">The document type.</param>
    /// <returns>The stable type identity.</returns>
    /// <example><code>var identity = DocumentTypeIdentity.For(typeof(Person));</code></example>
    public static DocumentTypeIdentity For(Type type)
    {
        EnsureArg.IsNotNull(type, nameof(type));
        return new(type.AssemblyQualifiedName ?? type.FullName ?? type.Name);
    }

    /// <inheritdoc />
    public override string ToString() => this.Value;
}

/// <summary>Identifies one named typed document client in keyed dependency injection.</summary>
/// <param name="DocumentType">The CLR document type.</param>
/// <param name="Name">The normalized client name.</param>
/// <example><code>var key = new DocumentStoreServiceKey(typeof(Person), "default");</code></example>
public readonly record struct DocumentStoreServiceKey(Type DocumentType, string Name);

/// <summary>
/// Contains provider-neutral identity, concurrency, integrity, timestamp, expiration, and custom-property metadata.
/// </summary>
/// <remarks>
/// Instances are snapshots. Providers and clients return cloned property bags so caller mutation cannot change persisted
/// state. Timestamps and expiration are normalized to UTC.
/// </remarks>
/// <example><code>var etag = info.ETag;</code></example>
public record DocumentInfo
{
    /// <summary>Gets the exact partition and row key identifying the document.</summary>
    /// <example><code>var partition = info.Key.PartitionKey;</code></example>
    public required DocumentKey Key { get; init; }

    /// <summary>Gets the opaque provider concurrency token used for conditional mutation.</summary>
    /// <example><code>var options = new DocumentWriteOptions { IfMatchETag = info.ETag };</code></example>
    public string ETag { get; init; }

    /// <summary>Gets the canonical SHA-256 hash of logical serialized content before payload transforms.</summary>
    /// <example><code>var hash = info.ContentHash;</code></example>
    public string ContentHash { get; init; }

    /// <summary>Gets the UTC timestamp at which the physical document was first created.</summary>
    /// <example><code>var createdAt = info.CreatedAt;</code></example>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp of the most recent content or metadata mutation.</summary>
    /// <example><code>var modifiedAt = info.LastModifiedAt;</code></example>
    public DateTimeOffset LastModifiedAt { get; init; }

    /// <summary>Gets the optional UTC instant at which the document becomes logically invisible.</summary>
    /// <example><code>var isTemporary = info.ExpiresAt is not null;</code></example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets a cloned bag of scalar application-defined properties.</summary>
    /// <example><code>var source = info.Properties.Get&lt;string&gt;("source");</code></example>
    public PropertyBag Properties { get; init; } = new();
}

/// <summary>Contains a deserialized document payload together with its provider-neutral metadata snapshot.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>Person person = entry.Value;</code></example>
public sealed record DocumentEntry<T> : DocumentInfo where T : class
{
    /// <summary>Gets the non-null deserialized application document.</summary>
    /// <example><code>Customer customer = entry.Value;</code></example>
    public required T Value { get; init; }
}

/// <summary>Configures concurrency, integrity, properties, and expiration for one document write.</summary>
/// <remarks>
/// By default an existing record is replaced while its expiration and properties are preserved. Create-only and ETag
/// conditions are mutually exclusive validation concerns enforced before provider mutation.
/// </remarks>
/// <example><code>var options = new DocumentWriteOptions { CreateOnly = true };</code></example>
public sealed record DocumentWriteOptions
{
    /// <summary>Gets the required current ETag for atomic conditional replacement, or null for an unconditional write.</summary>
    /// <example><code>var options = new DocumentWriteOptions { IfMatchETag = current.ETag };</code></example>
    public string IfMatchETag { get; init; }

    /// <summary>Gets whether creation must fail when any physical record, including an expired record, already exists.</summary>
    /// <example><code>var options = new DocumentWriteOptions { CreateOnly = true };</code></example>
    public bool CreateOnly { get; init; }

    /// <summary>Gets the optional canonical logical SHA-256 hash that serialized content must match before provider I/O.</summary>
    /// <example><code>var options = new DocumentWriteOptions { ExpectedContentHash = expectedHash };</code></example>
    public string ExpectedContentHash { get; init; }

    /// <summary>Gets the expiration mutation; the default preserves expiration on replacement and leaves inserts unexpired.</summary>
    /// <example><code>var options = new DocumentWriteOptions { Expiration = ExpirationChange.After(TimeSpan.FromHours(1)) };</code></example>
    public ExpirationChange Expiration { get; init; } = ExpirationChange.Preserve;

    /// <summary>Gets scalar application properties that replace the current bag; null preserves current properties.</summary>
    /// <example><code>var options = new DocumentWriteOptions { Properties = properties };</code></example>
    public PropertyBag Properties { get; init; }
}

/// <summary>Describes one keyed typed write and its options in an ordered batch.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <param name="Key">The partition and row key under which the value is stored.</param>
/// <param name="Value">The non-null application document to serialize.</param>
/// <param name="Options">Optional write conditions, integrity expectation, properties, and expiration.</param>
/// <example><code>var write = new DocumentWrite&lt;Person&gt;(key, person);</code></example>
public sealed record DocumentWrite<T>(DocumentKey Key, T Value, DocumentWriteOptions Options = null) where T : class;

/// <summary>Configures optional optimistic concurrency for one physical document deletion.</summary>
/// <example><code>var options = new DocumentDeleteOptions { IfMatchETag = info.ETag };</code></example>
public sealed record DocumentDeleteOptions
{
    /// <summary>Gets the required current ETag for conditional deletion, or null for idempotent unconditional deletion.</summary>
    /// <example><code>var options = new DocumentDeleteOptions { IfMatchETag = current.ETag };</code></example>
    public string IfMatchETag { get; init; }
}

/// <summary>Describes one exact-key physical deletion and its optional ETag condition in an ordered batch.</summary>
/// <param name="Key">The document key.</param>
/// <param name="Options">The delete options.</param>
/// <example><code>var delete = new DocumentDelete(key);</code></example>
public sealed record DocumentDelete(DocumentKey Key, DocumentDeleteOptions Options = null);

/// <summary>Describes an atomic custom-property and expiration update that leaves document content unchanged.</summary>
/// <param name="Key">The exact partition and row key identifying the document to update.</param>
/// <example><code>var update = new DocumentPropertiesUpdate(key) { Expiration = ExpirationChange.Clear };</code></example>
public sealed record DocumentPropertiesUpdate(DocumentKey Key)
{
    /// <summary>Gets scalar replacement properties; null preserves the current property bag.</summary>
    /// <example><code>var update = new DocumentPropertiesUpdate(key) { Properties = properties };</code></example>
    public PropertyBag Properties { get; init; }

    /// <summary>Gets the expiration mutation; the default preserves the current expiration.</summary>
    /// <example><code>var update = new DocumentPropertiesUpdate(key) { Expiration = ExpirationChange.Clear };</code></example>
    public ExpirationChange Expiration { get; init; } = ExpirationChange.Preserve;

    /// <summary>Gets the required current ETag for conditional update, or null for an unconditional metadata mutation.</summary>
    /// <example><code>var update = new DocumentPropertiesUpdate(key) { IfMatchETag = current.ETag };</code></example>
    public string IfMatchETag { get; init; }
}

/// <summary>Reports ordered successful items and failed keys for a complete or partially completed batch.</summary>
/// <typeparam name="T">The successful item type.</typeparam>
/// <example><code>var completed = batch.FailedKey is null;</code></example>
public sealed record DocumentBatchResult<T>
{
    /// <summary>Gets immutable successful results in the same order as their input operations.</summary>
    /// <example><code>foreach (var item in batch.Items) { Console.WriteLine(item); }</code></example>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Gets the first key whose operation failed, or null when every input operation completed.</summary>
    /// <example><code>var completed = batch.FailedKey is null;</code></example>
    public DocumentKey? FailedKey { get; init; }

    /// <summary>Gets all keys whose operations failed, in input order.</summary>
    /// <example><code>foreach (var key in batch.FailedKeys) { Console.WriteLine(key); }</code></example>
    public IReadOnlyList<DocumentKey> FailedKeys { get; init; } = [];
}

/// <summary>
/// Represents one provider-neutral serialized document returned across the persistence boundary.
/// </summary>
/// <remarks>
/// Content and metadata are provider-owned snapshots copied before return. The outer client verifies the stored hash,
/// reverses the recorded transform chain, verifies the logical hash, and only then deserializes the application value.
/// </remarks>
/// <example><code>var bytes = stored.Content;</code></example>
public sealed record StoredDocument
{
    /// <summary>Gets the exact partition and row key identifying the stored record.</summary>
    /// <example><code>var key = stored.Key;</code></example>
    public required DocumentKey Key { get; init; }

    /// <summary>Gets a copied transformed payload byte array.</summary>
    /// <example><code>var storedLength = stored.Content.Length;</code></example>
    public byte[] Content { get; init; } = [];

    /// <summary>Gets the opaque provider concurrency token for conditional mutation.</summary>
    /// <example><code>var etag = stored.ETag;</code></example>
    public string ETag { get; init; }

    /// <summary>Gets the canonical SHA-256 hash of logical serialized bytes before transforms.</summary>
    /// <example><code>var logicalHash = stored.ContentHash;</code></example>
    public string ContentHash { get; init; }

    /// <summary>Gets the canonical SHA-256 hash of <see cref="Content" /> as persisted.</summary>
    /// <example><code>var storedHash = stored.StoredContentHash;</code></example>
    public string StoredContentHash { get; init; }

    /// <summary>Gets the normalized UTC physical creation timestamp.</summary>
    /// <example><code>var createdAt = stored.CreatedAt;</code></example>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the normalized UTC timestamp of the latest content or metadata mutation.</summary>
    /// <example><code>var modifiedAt = stored.LastModifiedAt;</code></example>
    public DateTimeOffset LastModifiedAt { get; init; }

    /// <summary>Gets the optional normalized UTC logical expiration timestamp.</summary>
    /// <example><code>var expiresAt = stored.ExpiresAt;</code></example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets copied scalar application-defined properties.</summary>
    /// <example><code>var source = stored.Properties.Get&lt;string&gt;("source");</code></example>
    public PropertyBag Properties { get; init; } = new();

    /// <summary>Gets copied reserved metadata containing the versioned transform envelope.</summary>
    /// <example><code>var envelope = stored.TransformMetadata.Get&lt;string&gt;("bdk_transform_envelope");</code></example>
    public PropertyBag TransformMetadata { get; init; } = new();
}

/// <summary>Describes validated serialized bytes and metadata supplied by the outer client for one provider write.</summary>
/// <remarks>
/// Providers may trust that keys, hashes, size limits, property scalars, transform metadata, and option combinations were
/// validated before this model crossed the persistence boundary. Providers remain responsible for atomic commit and
/// backend-specific concurrency enforcement.
/// </remarks>
/// <example><code>var write = new StoredDocumentWrite { Key = key, Content = bytes };</code></example>
public sealed record StoredDocumentWrite
{
    /// <summary>Gets the exact partition and row key identifying the target record.</summary>
    /// <example><code>var key = write.Key;</code></example>
    public required DocumentKey Key { get; init; }

    /// <summary>Gets transformed payload bytes copied for persistence.</summary>
    /// <example><code>var length = write.Content.Length;</code></example>
    public byte[] Content { get; init; } = [];

    /// <summary>Gets the canonical SHA-256 hash of logical serialized bytes before transforms.</summary>
    /// <example><code>var logicalHash = write.ContentHash;</code></example>
    public string ContentHash { get; init; }

    /// <summary>Gets the canonical SHA-256 hash of <see cref="Content" />.</summary>
    /// <example><code>var storedHash = write.StoredContentHash;</code></example>
    public string StoredContentHash { get; init; }

    /// <summary>Gets copied scalar replacement properties; null instructs the provider to preserve current properties.</summary>
    /// <example><code>var replacesProperties = write.Properties is not null;</code></example>
    public PropertyBag Properties { get; init; }

    /// <summary>Gets copied reserved metadata describing the applied transform chain.</summary>
    /// <example><code>var metadata = write.TransformMetadata;</code></example>
    public PropertyBag TransformMetadata { get; init; } = new();

    /// <summary>Gets the absolute normalized UTC expiration resolved once by the outer client; null means no expiration.</summary>
    /// <example><code>var expiresAt = write.ExpiresAt;</code></example>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets whether replacement preserves current expiration and insertion creates an unexpired document.</summary>
    /// <example><code>if (write.PreserveExpiration) { /* retain the current value */ }</code></example>
    public bool PreserveExpiration { get; init; }

    /// <summary>Gets validated create-only, ETag, expected-hash, property, and expiration options for the operation.</summary>
    /// <example><code>var createOnly = write.Options.CreateOnly;</code></example>
    public DocumentWriteOptions Options { get; init; } = new();
}

/// <summary>Contains one bounded provider page of serialized documents and provider-native continuation state.</summary>
/// <example><code>var hasMore = page.ContinuationToken is not null;</code></example>
public sealed record StoredDocumentPage
{
    /// <summary>Gets immutable serialized document snapshots in deterministic key order.</summary>
    /// <example><code>foreach (var item in page.Items) { Console.WriteLine(item.Key); }</code></example>
    public IReadOnlyList<StoredDocument> Items { get; init; } = [];

    /// <summary>Gets opaque provider-native continuation state, or null when the native sequence is complete.</summary>
    /// <remarks>The outer client binds this value into a protected public continuation token; applications never receive it directly.</remarks>
    /// <example><code>var hasMore = page.ContinuationToken is not null;</code></example>
    public string ContinuationToken { get; init; }
}
