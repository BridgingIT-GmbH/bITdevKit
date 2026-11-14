// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Describes one authoritative immutable stored version, independently of its payload representation.</summary>
/// <example><code>var value = new CacheEntryMetadata();</code></example>
public sealed record CacheEntryMetadata
{
    /// <summary>Gets the exact canonical identity.</summary>
    public CacheEntryIdentity Identity { get; init; }

    /// <summary>Gets the original key for authorized, redacted operational inspection.</summary>
    public string DisplayKey { get; init; }

    /// <summary>Gets the stable application value-contract identifier.</summary>
    public string ContractId { get; init; }

    /// <summary>Gets the acquired/published opaque version.</summary>
    public CacheEntryVersion Version { get; init; }

    /// <summary>Gets whether this is a metadata-only confirmed-absence marker.</summary>
    public bool IsNotFound { get; init; }

    /// <summary>Gets the final dependency snapshot.</summary>
    public IReadOnlyList<CacheDimension> Dimensions { get; init; } = [];

    /// <summary>Gets the publication timestamp in UTC.</summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>Gets the effective logical expiry, enforced independently of cleanup.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Gets the absolute cap after one-time jitter.</summary>
    public DateTimeOffset AbsoluteExpiresAt { get; init; }

    /// <summary>Gets the idle timeout when authoritative sliding renewal is enabled.</summary>
    public TimeSpan? SlidingIdleTimeout { get; init; }

    /// <summary>Gets the authoritative touch or sampled activity observation, with declared accuracy.</summary>
    public DateTimeOffset LastAccessAt { get; init; }

    /// <summary>Gets whether a foreground access was observed. Publication and background work alone do not establish activity.</summary>
    public bool HasForegroundAccess { get; init; }

    /// <summary>Gets consecutive scheduler source failures for this exact version.</summary>
    public int RefreshFailureCount { get; init; }

    /// <summary>Gets the earliest next automatic refresh attempt for this version, or null when no backoff applies.</summary>
    public DateTimeOffset? RefreshNotBefore { get; init; }

    /// <summary>Gets bytes after reversing cache-managed transformations.</summary>
    public long LogicalSizeBytes { get; init; }

    /// <summary>Gets codec/raw bytes before compression.</summary>
    public long EncodedSizeBytes { get; init; }

    /// <summary>Gets exact stored representation bytes, including the persisted envelope.</summary>
    public long StoredSizeBytes { get; init; }

    /// <summary>Gets a conservative declared graph charge, or null when unknown.</summary>
    public long? DecodedSizeBytes { get; init; }

    /// <summary>Gets the optional domain item-count observation.</summary>
    public long? ItemCount { get; init; }

    /// <summary>Gets retention preference within ordinary limits.</summary>
    public CacheEvictionPriority Priority { get; init; } = CacheEvictionPriority.Normal;

    /// <summary>Gets the registered codec identifier, absent for raw binary.</summary>
    public string CodecId { get; init; }

    /// <summary>Gets the persisted codec representation version.</summary>
    public int CodecVersion { get; init; } = 1;

    /// <summary>Gets the persisted cache-managed algorithm, absent when uncompressed.</summary>
    public string Compression { get; init; }

    /// <summary>Gets the explicit payload envelope version.</summary>
    public int EnvelopeVersion { get; init; } = 1;

    /// <summary>Gets the logical content type.</summary>
    public string ContentType { get; init; }

    /// <summary>Gets the logical-byte SHA-256 integrity digest.</summary>
    public string PayloadHash { get; init; }

    /// <summary>Gets the stored-byte SHA-256 integrity digest.</summary>
    public string StoredHash { get; init; }

}