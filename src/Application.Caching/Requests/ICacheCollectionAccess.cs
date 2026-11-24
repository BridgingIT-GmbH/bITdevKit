// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Adapts a code-declared typed or binary collection for transport-neutral management without permitting loaders or arbitrary CLR types.</summary>
/// <example><code>var collection = registry.Get("cards");</code></example>
public interface ICacheCollectionAccess
{
    /// <summary>Gets the immutable declared binding.</summary>
    CacheCollectionBinding Binding { get; }

    /// <summary>Gets whether binary or exact-format validated encoded publication is approved for caller-owned streams.</summary>
    bool SupportsStreamWrites { get; }

    /// <summary>Acquires metadata-only authoritative validity and sliding activity for one compatible snapshot.</summary>
    ValueTask<Result<CacheLookup<CacheEntryMetadata>>> ReadMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Acquires a protected encoded/raw read without invoking a loader.</summary>
    ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options, CancellationToken cancellationToken = default);

    /// <summary>Publishes validated encoded or binary input through the collection's declared contract.</summary>
    ValueTask<Result<CacheWriteOutcome>> SetStreamAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken = default);

    /// <summary>Publishes a boundary-mapped value whose type exactly matches the trusted typed contract.</summary>
    ValueTask<Result<CacheWriteOutcome>> SetValueAsync(CacheItem item, object value, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken = default);

    /// <summary>Conditionally removes one exact key through the ordinary collection behavior chain.</summary>
    ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition, CancellationToken cancellationToken = default);

    /// <summary>Previews a bounded selection without payload reads.</summary>
    ValueTask<Result<CacheInvalidationPreview>> PreviewAsync(CacheSelection selection, CancellationToken cancellationToken = default);

    /// <summary>Invalidates a bounded selection or explicitly clears the logical collection.</summary>
    ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, bool clear, CancellationToken cancellationToken = default);

    /// <summary>Reads one bounded provider-indexed metadata page without opening payloads.</summary>
    ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheSelection selection, int take, string continuation, CancellationToken cancellationToken = default,
        DateTimeOffset? cutoff = null);
}