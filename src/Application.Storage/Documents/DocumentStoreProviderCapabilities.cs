// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes the persistence, query, concurrency, expiration, and projection capabilities of a document-store provider.
/// </summary>
/// <remarks>
/// Capabilities are immutable registration metadata. The outer client uses them to reject unsupported query shapes before
/// provider I/O, while health checks, diagnostics, and dashboards use them to describe effective behavior. A provider must
/// not advertise a capability unless every corresponding operation honors it.
/// </remarks>
/// <example>
/// <code>
/// var capabilities = new DocumentStoreProviderCapabilities
/// {
///     FullMatch = DocumentQuerySupport.SupportedEfficiently,
///     RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
///     SupportsContinuationPaging = true,
///     SupportsConditionalWrite = true,
///     SupportsLogicalExpiration = true
/// };
/// </code>
/// </example>
public sealed class DocumentStoreProviderCapabilities
{
    /// <summary>
    /// Gets the maximum transformed payload size accepted by the provider, in bytes, or <see langword="null" /> when the
    /// backend does not impose a lower known limit than the client configuration.
    /// </summary>
    /// <remarks>This limit applies after compression or encryption and excludes provider metadata unless documented otherwise.</remarks>
    /// <example><code>var limit = capabilities.MaxStoredDocumentSize;</code></example>
    public long? MaxStoredDocumentSize { get; init; }

    /// <summary>
    /// Gets whether create-only writes and atomic replacement guarded by <see cref="DocumentWriteOptions.IfMatchETag" />
    /// are supported.
    /// </summary>
    /// <example><code>if (capabilities.SupportsConditionalWrite) { /* use IfMatchETag */ }</code></example>
    public bool SupportsConditionalWrite { get; init; }

    /// <summary>
    /// Gets whether deletion guarded by <see cref="DocumentDeleteOptions.IfMatchETag" /> is supported atomically.
    /// </summary>
    /// <example><code>var canMoveSafely = capabilities.SupportsConditionalDelete;</code></example>
    public bool SupportsConditionalDelete { get; init; }

    /// <summary>
    /// Gets whether custom properties and expiration can be updated atomically without replacing document content.
    /// </summary>
    /// <example><code>var canPatchMetadata = capabilities.SupportsAtomicPropertyUpdate;</code></example>
    public bool SupportsAtomicPropertyUpdate { get; init; }

    /// <summary>
    /// Gets whether exact reads, queries, key listings, existence checks, and counts consistently exclude documents due at
    /// the supplied visibility cutoff.
    /// </summary>
    /// <example><code>var expirationIsFiltered = capabilities.SupportsLogicalExpiration;</code></example>
    public bool SupportsLogicalExpiration { get; init; }

    /// <summary>
    /// Gets whether the provider implements bounded physical cleanup through <see cref="IDocumentStoreRetentionProvider" />
    /// or equivalent native expiration support.
    /// </summary>
    /// <example><code>var canSweepExpired = capabilities.SupportsRetention;</code></example>
    public bool SupportsRetention { get; init; }

    /// <summary>
    /// Gets support for exact partition-key and row-key matching.
    /// </summary>
    /// <example><code>var support = capabilities.FullMatch;</code></example>
    public DocumentQuerySupport FullMatch { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets support for matching a row-key prefix within one partition.
    /// </summary>
    /// <example><code>var support = capabilities.RowKeyPrefixMatch;</code></example>
    public DocumentQuerySupport RowKeyPrefixMatch { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets support for matching a row-key suffix within one partition.
    /// </summary>
    /// <example><code>var support = capabilities.RowKeySuffixMatch;</code></example>
    public DocumentQuerySupport RowKeySuffixMatch { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets support for explicitly approved type-wide scans without a key constraint.
    /// </summary>
    /// <example><code>var support = capabilities.FullScan;</code></example>
    public DocumentQuerySupport FullScan { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets support for bounded key-only listing.
    /// </summary>
    /// <example><code>var support = capabilities.KeyListing;</code></example>
    public DocumentQuerySupport KeyListing { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets whether bounded queries can return and consume provider-native continuation state.
    /// </summary>
    /// <remarks>The public client protects and binds native state inside an opaque Document Storage continuation token.</remarks>
    /// <example><code>var canContinue = capabilities.SupportsContinuationPaging;</code></example>
    public bool SupportsContinuationPaging { get; init; }

    /// <summary>
    /// Gets whether count queries execute in the backend without enumerating matching document records in application code.
    /// </summary>
    /// <example><code>var countIsNative = capabilities.SupportsServerSideCount;</code></example>
    public bool SupportsServerSideCount { get; init; }

    /// <summary>
    /// Gets whether key-only pages avoid loading or materializing stored payload bytes.
    /// </summary>
    /// <example><code>var listingAvoidsPayloads = capabilities.SupportsKeyOnlyProjection;</code></example>
    public bool SupportsKeyOnlyProjection { get; init; }
}
