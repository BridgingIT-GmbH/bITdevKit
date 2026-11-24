// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Defines the authoritative shard generation, committed journal prefix and newest-first immutable segment set.</summary>
/// <example><code>manifest.Validate(storeId, collectionIdentity, shard); var committedBytes = manifest.JournalBytes;</code></example>
public sealed record CacheManifest
{
    /// <summary>The maximum of 64 immutable segments referenced by one authoritative shard manifest.</summary>
    public const int MaximumSegments = 64;

    /// <summary>Gets the manifest serialization format, currently one.</summary>
    public int Format { get; init; } = 1;

    /// <summary>Gets the generated store identity that prevents records from another root being adopted.</summary>
    public string StoreId { get; init; }

    /// <summary>Gets the canonical logical collection identity owning this shard.</summary>
    public string CollectionIdentity { get; init; }

    /// <summary>Gets the fixed shard number from zero through 255.</summary>
    public int Shard { get; init; }

    /// <summary>Gets the durable offline-recovery generation authorizing this manifest.</summary>
    public long RecoveryGeneration { get; init; }

    /// <summary>Gets the generated identity of this exact authoritative manifest publication.</summary>
    public string Generation { get; init; }

    /// <summary>Gets the last committed shard mutation sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the sequence represented by the checkpoint before the current journal prefix.</summary>
    public long CheckpointSequence { get; init; }

    /// <summary>Gets the shard&apos;s collection invalidation boundary.</summary>
    public long Epoch { get; init; }

    /// <summary>Gets the generated identifier of the current journal file.</summary>
    public string Journal { get; init; }

    /// <summary>Gets the authoritative committed prefix length; bytes beyond it are uncommitted.</summary>
    public long JournalBytes { get; init; }

    /// <summary>Gets conservative metadata attribution accumulated by the currently referenced index publications.</summary>
    public long MetadataCharge { get; init; }

    /// <summary>Gets immutable index references in newest-first publication order.</summary>
    public IReadOnlyList<CacheSegmentReference> Segments { get; init; } = [];

    /// <summary>Gets the operation receipt associated with the current manifest publication.</summary>
    public string OperationId { get; init; }

    /// <summary>Rejects mismatched store, collection or shard identity, invalid generated names, inconsistent sequences and excessive segment sets.</summary>
    public void Validate(string storeId, string collectionIdentity, int shard)
    {
        if (this.Format != 1 || this.StoreId != storeId || this.CollectionIdentity != collectionIdentity || this.Shard != shard || shard is < 0 or > 255 ||
            !Guid.TryParseExact(this.StoreId, "N", out _) || !Guid.TryParseExact(this.Generation, "N", out _) || !Guid.TryParseExact(this.Journal, "N", out _) ||
            (this.OperationId is not null && !Guid.TryParseExact(this.OperationId, "N", out _)) || this.Sequence < 0 ||
            this.CheckpointSequence < 0 || this.CheckpointSequence > this.Sequence || this.RecoveryGeneration < 0 || this.Epoch < 0 ||
            this.JournalBytes is < 0 or > 32 * CacheDefaults.MiB || this.MetadataCharge < 0 || this.Segments is null || this.Segments.Count > MaximumSegments ||
            this.Segments.Any(s => s is null || !s.Valid(this.Sequence)) || this.Segments.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != this.Segments.Count)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        var previous = long.MaxValue;
        foreach (var segment in this.Segments)
        {
            if (segment.Sequence >= previous) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            previous = segment.Sequence;
        }
    }
}

/// <summary>Identifies immutable index and record files with their publication sequence, exact lengths and full SHA-256 digests.</summary>
/// <example><code>var valid = segment.Valid(manifest.Sequence);</code></example>
public sealed record CacheSegmentReference(string Id, long Sequence, long IndexBytes, long RecordsBytes, string IndexHash, string RecordsHash)
{
    /// <summary>Checks generated naming, hash shapes, nonnegative lengths and a sequence within the supplied committed boundary.</summary>
    public bool Valid(long committedSequence) => Guid.TryParseExact(this.Id, "N", out _) && this.Sequence >= 0 && this.Sequence <= committedSequence &&
        this.IndexBytes > 0 && this.RecordsBytes >= 0 && Hash(this.IndexHash) && Hash(this.RecordsHash);

    private static bool Hash(string value) => value?.Length == 64 && value.All(c => char.IsAsciiHexDigit(c));
}