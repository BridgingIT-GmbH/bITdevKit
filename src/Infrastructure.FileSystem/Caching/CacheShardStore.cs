// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Opens one fixed collection shard under native authority and validates its checked bootstrap header and authoritative manifest.</summary>
/// <example><code>await using var transaction = await shard.AcquireAsync(false, null, null, token);</code></example>
public sealed class CacheShardStore(CacheStoreRoot root, string directory, string storeId, string collectionIdentity, int shard,
    long recoveryGeneration, ResourceBudget transient, ResourceBudget resident, CacheIndexBlockCache blocks, bool forceDurableMetadata = false)
{
    /// <summary>Gets the dedicated physical directory containing this shard&apos;s checked files.</summary>
    public string DirectoryPath { get; } = directory;

    /// <summary>Gets the native access, durability and quota profile inherited from the store root.</summary>
    public FileSystemCacheOptions Profile => root.Profile;

    /// <summary>Gets whether this shard requires durable metadata acknowledgement, including the always-durable root control ledger.</summary>
    public bool Durable => forceDurableMetadata || this.Profile.Durable;

    /// <summary>Gets whether this shard is the reserved root control ledger with no application payload readers.</summary>
    public bool ControlLedger => forceDurableMetadata && collectionIdentity == "root-ledger" && shard == 0;

    /// <summary>Gets or sets an optional test fault hook invoked at defined shard publication boundaries.</summary>
    public Action<string> Fault { get; set; }

    /// <summary>Acquires native authority, optionally bootstraps an admitted empty shard, and rejects missing or incompatible committed metadata without adopting older files.</summary>
    /// <param name="create">Whether an admitted mutation may initialize an unpublished shard.</param>
    /// <param name="initialEpoch">Reads collection authority after the new directory becomes visible.</param>
    /// <param name="reserveBootstrap">Admits bounded bootstrap metadata before creating it.</param>
    /// <param name="cancellationToken">Cancels native authority acquisition and metadata I/O.</param>
    /// <param name="isUnpublished">Optionally proves that no durable root publication receipt exists. Reads check this proof under shard authority for a stages-only directory, or without a shard lock when the entire directory is absent.</param>
    /// <returns>The protected initialized shard, or null for an absent or proven unpublished shard.</returns>
    public async ValueTask<CacheShardTransaction> AcquireAsync(bool create, Func<CancellationToken, ValueTask<long>> initialEpoch,
        Func<long, CancellationToken, ValueTask<Result>> reserveBootstrap, CancellationToken cancellationToken,
        Func<CancellationToken, ValueTask<bool>> isUnpublished = null)
    {
        FileSystemPaths.RejectLinks(this.DirectoryPath);
        if (!Directory.Exists(this.DirectoryPath))
        {
            if (!create)
            {
                if (isUnpublished is null || await isUnpublished(cancellationToken).ConfigureAwait(false)) { return null; }

                // The root proof owns no shard lock. A concurrent first publication may have made
                // the directory visible while it was read; acquire and verify that directory below.
                if (!Directory.Exists(this.DirectoryPath)) { throw new CacheTransferException(CacheErrorCode.Corruption); }
            }

            // Directory visibility precedes the initialization-epoch read. A collection invalidator
            // can therefore either fence this existing shard or make its new root epoch observable here.
            if (create) { FileSystemDurability.EnsureDirectory(this.DirectoryPath); }
        }

        var authority = await FileSystemAuthority.AcquireAsync(this.Profile.AuthorityOptions, this.DirectoryPath, "authority", false, cancellationToken).ConfigureAwait(false);
        if (authority is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

        CacheMetadataDocument<CacheManifest> manifest = null;
        var handedOff = false;
        try
        {
            var headerPath = Path.Combine(this.DirectoryPath, "header.json");
            if (!File.Exists(headerPath))
            {
                if (Directory.EnumerateFileSystemEntries(this.DirectoryPath).Any(p =>
                {
                    if (Path.GetFileName(p) == "authority") { return false; }

                    FileSystemPaths.RejectLinks(p);
                    return Path.GetFileName(p) != "stages" || !Directory.Exists(p);
                }) || isUnpublished is not null && !await isUnpublished(cancellationToken).ConfigureAwait(false))
                {
                    throw new CacheTransferException(CacheErrorCode.Corruption);
                }

                if (!create)
                {
                    if (isUnpublished is null) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                    // A mutation exposes its stage directory before initializing the first manifest.
                    // The root proof is read while authority prevents that mutation from publishing.
                    return null;
                }

                var reserved = await reserveBootstrap(2 * FileSystemCacheOptions.MaximumMetadataBytes, cancellationToken).ConfigureAwait(false);
                if (reserved.IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

                var header = new CacheShardHeader(1, storeId, collectionIdentity, shard, recoveryGeneration, false);
                await CacheCheckedMetadata.WriteAsync(this.DirectoryPath, "header.json", header, authority, true, transient, resident, cancellationToken).ConfigureAwait(false);
                var journal = Guid.NewGuid().ToString("N");
                var temporary = Path.Combine(this.DirectoryPath, journal + ".journal.tmp");
                await using (var file = FileSystemFileOperations.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                    FileSystemDurability.Flush(file);
                }

                FileSystemDurability.Move(temporary, Path.Combine(this.DirectoryPath, journal + ".journal"), false, true);
                var epoch = await initialEpoch(cancellationToken).ConfigureAwait(false);
                var first = new CacheManifest { StoreId = storeId, CollectionIdentity = collectionIdentity, Shard = shard, RecoveryGeneration = recoveryGeneration,
                    Generation = Guid.NewGuid().ToString("N"), Journal = journal, Epoch = epoch };
                await CacheCheckedMetadata.WriteAsync(this.DirectoryPath, "manifest.json", first, authority, true, transient, resident, cancellationToken).ConfigureAwait(false);
                await CacheCheckedMetadata.WriteAsync(this.DirectoryPath, "header.json", header with { Initialized = true }, authority,
                    true, transient, resident, cancellationToken).ConfigureAwait(false);
            }

            using (var header = await CacheCheckedMetadata.ReadAsync<CacheShardHeader>(headerPath, transient, resident, cancellationToken).ConfigureAwait(false))
            {
                if (header.Value != new CacheShardHeader(1, storeId, collectionIdentity, shard, recoveryGeneration, true))
                {
                    throw new CacheTransferException(header.Value.Initialized ? CacheErrorCode.Compatibility : CacheErrorCode.ProviderUnavailable);
                }
            }

            try { manifest = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(this.DirectoryPath, "manifest.json"), transient, resident, cancellationToken).ConfigureAwait(false); }
            catch (FileNotFoundException) { throw new CacheTransferException(CacheErrorCode.Corruption); }

            manifest.Value.Validate(storeId, collectionIdentity, shard);
            if (manifest.Value.RecoveryGeneration != recoveryGeneration) { throw new CacheTransferException(CacheErrorCode.Compatibility); }

            var transaction = new CacheShardTransaction(this, authority, manifest, transient, resident, blocks);
            handedOff = true;
            return transaction;
        }
        finally
        {
            if (!handedOff) { manifest?.Dispose(); authority.Dispose(); }
        }
    }

    private sealed record CacheShardHeader(int Format, string StoreId, string CollectionIdentity, int Shard, long RecoveryGeneration, bool Initialized);
}