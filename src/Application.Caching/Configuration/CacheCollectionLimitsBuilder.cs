// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records explicit collection resource ceilings without replacing unconfigured sibling limits.</summary>
/// <example><code>limits.MaxEntries(1000).MaxPayloadBytes(ByteSize.Megabytes(2));</code></example>
public sealed class CacheCollectionLimitsBuilder : PatchOptionsBuilder<CacheCollectionLimits, CacheCollectionLimitsBuilder>
{
    /// <summary>Sets the collection cache-owned resident byte ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxResidentBytes(ByteSize.Megabytes(64));</code></example>
    public CacheCollectionLimitsBuilder MaxResidentBytes(long value) => this.With(options => options.MaxResidentBytes, value);

    /// <summary>Sets current, staged, and retired storage capacity.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxStorageBytes(ByteSize.Megabytes(128));</code></example>
    public CacheCollectionLimitsBuilder MaxStorageBytes(long value) => this.With(options => options.MaxStorageBytes, value);

    /// <summary>Sets the retained current entry ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxEntries(1000);</code></example>
    public CacheCollectionLimitsBuilder MaxEntries(long value) => this.With(options => options.MaxEntries, value);

    /// <summary>Sets the single encoded payload ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxEncodedBytes(ByteSize.Megabytes(2));</code></example>
    public CacheCollectionLimitsBuilder MaxEncodedBytes(long value) => this.With(options => options.MaxEncodedBytes, value);

    /// <summary>Sets the logical or decompressed payload ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxLogicalBytes(ByteSize.Megabytes(4));</code></example>
    public CacheCollectionLimitsBuilder MaxLogicalBytes(long value) => this.With(options => options.MaxLogicalBytes, value);

    /// <summary>Sets the conservative materialized graph ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxDecodedBytes(ByteSize.Megabytes(16));</code></example>
    public CacheCollectionLimitsBuilder MaxDecodedBytes(long value) => this.With(options => options.MaxDecodedBytes, value);

    /// <summary>Sets simultaneous source executions for the collection.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxActiveSources(4);</code></example>
    public CacheCollectionLimitsBuilder MaxActiveSources(int value) => this.With(options => options.MaxActiveSources, value);

    /// <summary>Sets queued source admissions for the collection.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxQueuedSources(16);</code></example>
    public CacheCollectionLimitsBuilder MaxQueuedSources(int value) => this.With(options => options.MaxQueuedSources, value);

    /// <summary>Sets simultaneous local waiters for one source flight.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxFollowersPerKey(64);</code></example>
    public CacheCollectionLimitsBuilder MaxFollowersPerKey(int value) => this.With(options => options.MaxFollowersPerKey, value);

    /// <summary>Sets simultaneous collection provider I/O operations.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxActiveProviderOperations(8);</code></example>
    public CacheCollectionLimitsBuilder MaxActiveProviderOperations(int value) => this.With(options => options.MaxActiveProviderOperations, value);

    /// <summary>Sets queued collection provider I/O admissions.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxQueuedProviderOperations(16);</code></example>
    public CacheCollectionLimitsBuilder MaxQueuedProviderOperations(int value) => this.With(options => options.MaxQueuedProviderOperations, value);

    /// <summary>Sets simultaneous collection metadata queries.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxMetadataQueries(2);</code></example>
    public CacheCollectionLimitsBuilder MaxMetadataQueries(int value) => this.With(options => options.MaxMetadataQueries, value);

    /// <summary>Sets simultaneous occupied collection transfers.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxTransfers(4);</code></example>
    public CacheCollectionLimitsBuilder MaxTransfers(int value) => this.With(options => options.MaxTransfers, value);

    /// <summary>Sets the normal owned source batch target.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.BatchSize(32);</code></example>
    public CacheCollectionLimitsBuilder BatchSize(int value) => this.With(options => options.BatchSize, value);

    /// <summary>Sets the hard ordered-batch admission bound.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxBatchSize(128);</code></example>
    public CacheCollectionLimitsBuilder MaxBatchSize(int value) => this.With(options => options.MaxBatchSize, value);

    /// <summary>Sets the normal indexed metadata page size.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MetadataPageSize(100);</code></example>
    public CacheCollectionLimitsBuilder MetadataPageSize(int value) => this.With(options => options.MetadataPageSize, value);

    /// <summary>Sets the total candidate allowance for one metadata selection.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxQueryCandidates(10000);</code></example>
    public CacheCollectionLimitsBuilder MaxQueryCandidates(int value) => this.With(options => options.MaxQueryCandidates, value);

    /// <summary>Sets the metadata selection work budget.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxQueryWork(TimeSpan.FromSeconds(1));</code></example>
    public CacheCollectionLimitsBuilder MaxQueryWork(TimeSpan value) => this.With(options => options.MaxQueryWork, value);

    /// <summary>Sets the UTF-8 key byte ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxKeyBytes(1024);</code></example>
    public CacheCollectionLimitsBuilder MaxKeyBytes(int value) => this.With(options => options.MaxKeyBytes, value);

    /// <summary>Sets the retained metadata ceiling for one entry.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxEntryMetadataBytes(ByteSize.Kilobytes(32));</code></example>
    public CacheCollectionLimitsBuilder MaxEntryMetadataBytes(long value) => this.With(options => options.MaxEntryMetadataBytes, value);

    /// <summary>Sets the collection background-refresh source sublimit.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxRefreshSources(1);</code></example>
    public CacheCollectionLimitsBuilder MaxRefreshSources(int value) => this.With(options => options.MaxRefreshSources, value);

    /// <summary>Sets both encoded and logical payload ceilings to the same explicit byte bound.</summary>
    /// <param name="bytes">The shared encoded and logical ceiling.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxPayloadBytes(ByteSize.Megabytes(2));</code></example>
    public CacheCollectionLimitsBuilder MaxPayloadBytes(long bytes) => this.With(options => options.MaxEncodedBytes, bytes).With(options => options.MaxLogicalBytes, bytes);
}