// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines finite child ceilings. A smaller applicable parent/provider ceiling always wins.</summary>
/// <example><code>var options = new CacheCollectionLimits();</code></example>
public sealed record CacheCollectionLimits
{
    /// <summary>Gets collection resident charge ceiling.</summary>
    public long MaxResidentBytes { get; init; } = 128 * CacheDefaults.MiB;

    /// <summary>Gets stored representation capacity; persistent profiles default to ten GiB.</summary>
    public long MaxStorageBytes { get; init; } = 128 * CacheDefaults.MiB;

    /// <summary>Gets current retained entries; persistent profiles default to one million.</summary>
    public long MaxEntries { get; init; } = 100000;

    /// <summary>Gets single codec/raw payload limit; persistent profiles default to 256 MiB.</summary>
    public long MaxEncodedBytes { get; init; } = 4 * CacheDefaults.MiB;

    /// <summary>Gets logical/decompressed size limit.</summary>
    public long MaxLogicalBytes { get; init; } = 4 * CacheDefaults.MiB;

    /// <summary>Gets approved conservative typed graph ceiling.</summary>
    public long MaxDecodedBytes { get; init; } = 16 * CacheDefaults.MiB;

    /// <summary>Gets concurrent source executions.</summary>
    public int MaxActiveSources { get; init; } = 8;

    /// <summary>Gets queued source admissions.</summary>
    public int MaxQueuedSources { get; init; } = 64;

    /// <summary>Gets concurrent waiters for one local flight.</summary>
    public int MaxFollowersPerKey { get; init; } = 128;

    /// <summary>Gets occupied provider I/O units.</summary>
    public int MaxActiveProviderOperations { get; init; } = 16;

    /// <summary>Gets queued provider units.</summary>
    public int MaxQueuedProviderOperations { get; init; } = 64;

    /// <summary>Gets concurrent metadata queries.</summary>
    public int MaxMetadataQueries { get; init; } = 2;

    /// <summary>Gets concurrent occupied transfers.</summary>
    public int MaxTransfers { get; init; } = 4;

    /// <summary>Gets normal batch target.</summary>
    public int BatchSize { get; init; } = 128;

    /// <summary>Gets hard ordered-batch admission bound.</summary>
    public int MaxBatchSize { get; init; } = 1024;

    /// <summary>Gets default bounded metadata page.</summary>
    public int MetadataPageSize { get; init; } = 100;

    /// <summary>Gets hard metadata entry page bound.</summary>
    public int MaxMetadataPageSize { get; init; } = 1000;

    /// <summary>Gets hard metadata byte page bound.</summary>
    public long MaxMetadataPageBytes { get; init; } = 4 * CacheDefaults.MiB;

    /// <summary>Gets selection node limit.</summary>
    public int MaxSelectionClauses { get; init; } = 32;

    /// <summary>Gets selection nesting limit.</summary>
    public int MaxSelectionDepth { get; init; } = 8;

    /// <summary>Gets dimension membership operand limit.</summary>
    public int MaxMembershipValues { get; init; } = 256;

    /// <summary>Gets candidate budget for a complete query, including continuation.</summary>
    public int MaxQueryCandidates { get; init; } = 100000;

    /// <summary>Gets maximum new query work per operation.</summary>
    public TimeSpan MaxQueryWork { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets uTF-8 key ceiling.</summary>
    public int MaxKeyBytes { get; init; } = 4096;

    /// <summary>Gets entry dependency count.</summary>
    public int MaxDimensions { get; init; } = 16;

    /// <summary>Gets final serialized metadata ceiling.</summary>
    public long MaxEntryMetadataBytes { get; init; } = 64 * CacheDefaults.KiB;

    /// <summary>Gets background refresh source sublimit.</summary>
    public int MaxRefreshSources { get; init; } = 2;
}