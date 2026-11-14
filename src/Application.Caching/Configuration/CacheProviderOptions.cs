// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines one named complete store's finite native policies. Secrets never enter diagnostics.</summary>
/// <example><code>var options = new CacheProviderOptions();</code></example>
public sealed record CacheProviderOptions
{
    /// <summary>Gets the registered complete provider kind.</summary>
    public string Kind { get; init; } = "InMemory";

    /// <summary>Gets the registered native database engine profile, Sqlite, SqlServer, or PostgreSql.</summary>
    public string Engine { get; init; } = "Sqlite";

    /// <summary>Gets the provider resident capacity.</summary>
    public long MaxResidentBytes { get; init; } = 128 * CacheDefaults.MiB;

    /// <summary>Gets the persistent retained plus staged and retired storage ceiling.</summary>
    public long MaxStorageBytes { get; init; } = 10 * CacheDefaults.GiB;

    /// <summary>Gets the provider current entry ceiling.</summary>
    public long MaxEntries { get; init; } = 10000000;

    /// <summary>Gets the resident metadata/index cache ceiling within resident charges.</summary>
    public long MetadataCacheBytes { get; init; } = 16 * CacheDefaults.MiB;

    /// <summary>Gets the storage capacity reserved for metadata/reclamation.</summary>
    public double MetadataReserveRatio { get; init; } = 0.05;

    /// <summary>Gets the bounded metadata, open, authority, or chunk attempt budget.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets the default-off infrastructure-only circuit breaker.</summary>
    public CacheProviderBreakerOptions Breaker { get; init; } = new();

    /// <summary>Gets the EF small payload inline threshold.</summary>
    public int InlinePayloadThreshold { get; init; } = 64 * (int)CacheDefaults.KiB;

    /// <summary>Gets the maximum native persisted payload chunk.</summary>
    public int ChunkSize { get; init; } = CacheDefaults.MaximumChunkBytes;

    /// <summary>Gets the bounded shared ownership lease.</summary>
    public TimeSpan LeaseLifetime { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the shared owner renewal interval.</summary>
    public TimeSpan LeaseRenewalInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the host-supplied database setting excluded from diagnostic snapshots.</summary>
    public string ConnectionString { get; init; } = null;

    /// <summary>Gets the trusted host database readiness registration.</summary>
    public string DatabaseReadyAlias { get; init; } = null;

    /// <summary>Gets the provider-owned absolute filesystem root.</summary>
    public string RootPath { get; init; } = null;

    /// <summary>Gets the declared Local, Smb, or Nfs filesystem profile.</summary>
    public string AccessKind { get; init; } = "Local";

    /// <summary>Gets the declared Durable or Buffered acknowledgement profile.</summary>
    public string Durability { get; init; } = "Durable";

    /// <summary>Gets the interruptible filesystem authority acquisition budget.</summary>
    public TimeSpan LockAcquireTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets the format-one lazy immutable filesystem shard count.</summary>
    public int ShardCount { get; init; } = 256;

    /// <summary>Gets the filesystem metadata/index block bound.</summary>
    public int MetadataBlockBytes { get; init; } = 64 * (int)CacheDefaults.KiB;

    /// <summary>Gets the filesystem checkpoint target.</summary>
    public long CheckpointBytes { get; init; } = 16 * CacheDefaults.MiB;

    /// <summary>Gets the filesystem hard uncheckpointed journal tail bound.</summary>
    public long MaximumJournalTailBytes { get; init; } = 32 * CacheDefaults.MiB;

    /// <summary>Gets the maximum filesystem quota credit block.</summary>
    public long QuotaCreditBytes { get; init; } = 8 * CacheDefaults.MiB;

    /// <summary>Gets the provider-owned metadata format version.</summary>
    public int MetadataFormatVersion { get; init; } = 1;

    /// <summary>Gets the local-host SQLite bounded busy budget.</summary>
    public TimeSpan SqliteBusyTimeout { get; init; } = TimeSpan.FromSeconds(5);
}