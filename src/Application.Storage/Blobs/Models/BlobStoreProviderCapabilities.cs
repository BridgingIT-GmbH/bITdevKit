// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes blob-store provider capabilities used by validation and diagnostics.
/// </summary>
/// <example>
/// <code>
/// var capabilities = new BlobStoreProviderCapabilities
/// {
///     SupportsContinuationPaging = true,
///     SupportsPrefixListing = true
/// };
/// </code>
/// </example>
public sealed class BlobStoreProviderCapabilities
{
    /// <summary>
    /// Gets a value indicating whether continuation paging is supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsContinuationPaging;
    /// </code>
    /// </example>
    public bool SupportsContinuationPaging { get; init; }

    /// <summary>
    /// Gets a value indicating whether prefix listing is supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsPrefixListing;
    /// </code>
    /// </example>
    public bool SupportsPrefixListing { get; init; }

    /// <summary>
    /// Gets a value indicating whether full container scans are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsFullContainerScan;
    /// </code>
    /// </example>
    public bool SupportsFullContainerScan { get; init; }

    /// <summary>
    /// Gets a value indicating whether custom properties are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsProperties;
    /// </code>
    /// </example>
    public bool SupportsProperties { get; init; }

    /// <summary>
    /// Gets a value indicating whether content type storage is supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsContentType;
    /// </code>
    /// </example>
    public bool SupportsContentType { get; init; }

    /// <summary>
    /// Gets a value indicating whether entity tags are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsETag;
    /// </code>
    /// </example>
    public bool SupportsETag { get; init; }

    /// <summary>
    /// Gets a value indicating whether provider-neutral content hashes are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsContentHash;
    /// </code>
    /// </example>
    public bool SupportsContentHash { get; init; }

    /// <summary>
    /// Gets a value indicating whether native provider leases are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsNativeLeases;
    /// </code>
    /// </example>
    public bool SupportsNativeLeases { get; init; }

    /// <summary>
    /// Gets a value indicating whether internal provider leases are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsInternalLeases;
    /// </code>
    /// </example>
    public bool SupportsInternalLeases { get; init; }

    /// <summary>
    /// Gets a value indicating whether conditional property updates are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsConditionalPropertiesUpdate;
    /// </code>
    /// </example>
    public bool SupportsConditionalPropertiesUpdate { get; init; }

    /// <summary>
    /// Gets a value indicating whether streaming uploads are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsStreamingUpload;
    /// </code>
    /// </example>
    public bool SupportsStreamingUpload { get; init; }

    /// <summary>
    /// Gets a value indicating whether streaming downloads are supported.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsStreamingDownload;
    /// </code>
    /// </example>
    public bool SupportsStreamingDownload { get; init; }

    /// <summary>
    /// Gets a value indicating whether blob expiration timestamps are persisted by the provider.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsExpiration;
    /// </code>
    /// </example>
    public bool SupportsExpiration { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider can sweep expired blobs without broad public listing.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = capabilities.SupportsRetentionSweep;
    /// </code>
    /// </example>
    public bool SupportsRetentionSweep { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider can use native backend retention indexes or lifecycle features.
    /// </summary>
    /// <example>
    /// <code>
    /// var nativeRetention = capabilities.SupportsNativeRetention;
    /// </code>
    /// </example>
    public bool SupportsNativeRetention { get; init; }
}
