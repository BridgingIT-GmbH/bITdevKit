// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

// Every registration happens under the same shard authority as retirement. Native owner liveness,
// rather than a deadline or process id, determines whether physical deletion is safe.
/// <summary>Creates durable bounded reader pins under shard authority and uses native liveness proof before allowing physical reclamation.</summary>
/// <example><code>await using var pin = await registry.RegisterAsync(authority, &quot;payload&quot;, new[] { version }, token);</code></example>
public sealed class CacheReaderRegistry(string shardDirectory, string storeId, string collectionIdentity, int shard, long recoveryGeneration, FileSystemCacheOptions profile,
    ResourceBudget transient, ResourceBudget resident,
    Func<string, long, CancellationToken, ValueTask<Result>> admit, Func<string, long, CancellationToken, ValueTask> release, int maximumPins = 4096)
{
    /// <summary>The 8,192-byte conservative durable space reservation for each reader pin.</summary>
    public const int PinCreditBytes = 8192;
    private readonly string directory = Path.Combine(shardDirectory, "pins");

    /// <summary>Admits and publishes a reader pin for validated payload or index references; the returned lease owns the pin until actual disposal completes.</summary>
    public async ValueTask<IAsyncDisposable> RegisterAsync(FileSystemAuthority shardAuthority, string kind, IReadOnlyList<string> references,
        CancellationToken cancellationToken)
    {
        shardAuthority.EnsureHeld(shardDirectory);
        if (kind is not ("payload" or "index") || references is null || references.Count is <= 0 or > CacheManifest.MaximumSegments ||
            references.Any(r => !Guid.TryParseExact(r, "N", out _)) || references.Distinct(StringComparer.Ordinal).Count() != references.Count ||
            maximumPins is <= 0 or > 4096 || string.IsNullOrEmpty(collectionIdentity) || shard is < 0 or > 255)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        FileSystemPaths.RejectLinks(this.directory);
        if (Directory.Exists(this.directory) && Directory.EnumerateFiles(this.directory, "*.pin").Take(maximumPins).Count() >= maximumPins)
        {
            throw new CacheTransferException(CacheErrorCode.Capacity);
        }

        if (!ResourceCharge.TryReserve(16384, out var memory, transient, resident)) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        FileSystemAuthority owner = null;
        try
        {
            var id = Guid.NewGuid().ToString("N");
            if ((await admit(id, PinCreditBytes, cancellationToken).ConfigureAwait(false)).IsFailure) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            FileSystemDurability.EnsureDirectory(this.directory);
            owner = await FileSystemAuthority.AcquireAsync(profile.AuthorityOptions, this.directory, "stage-reader-" + id, true, cancellationToken).ConfigureAwait(false);
            if (owner is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            var record = new CacheReaderPin(1, storeId, collectionIdentity, shard, recoveryGeneration, id, kind, references.ToArray());
            // Pin ownership is recovery metadata and is durable even when cache payload acknowledgement is Buffered.
            await CacheCheckedMetadata.WriteAsync(this.directory, id + ".pin", record, owner, true, transient, resident, cancellationToken).ConfigureAwait(false);
            return new Lease(this, owner, id, memory);
        }
        catch
        {
            // An uncertain record may exist. Leave its credit charged and shared ownership in place.
            owner?.RetainIfShared();
            owner?.Dispose();
            memory.Dispose();
            throw;
        }
    }

    /// <summary>Checks bounded pin records under shard authority; shared pins remain live, and local orphan pins are removed only after exclusive native-owner proof.</summary>
    public async ValueTask<bool> IsPinnedAsync(FileSystemAuthority shardAuthority, string kind, string reference, CancellationToken cancellationToken)
    {
        shardAuthority.EnsureHeld(shardDirectory);
        if (kind is not ("payload" or "index") || !Guid.TryParseExact(reference, "N", out _) || maximumPins is <= 0 or > 4096)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        FileSystemPaths.RejectLinks(this.directory);
        if (!Directory.Exists(this.directory)) { return false; }

        var examined = 0;
        foreach (var path in Directory.EnumerateFiles(this.directory, "*.pin"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++examined > maximumPins) { throw new CacheTransferException(CacheErrorCode.Capacity); }

            using var document = await CacheCheckedMetadata.ReadAsync<CacheReaderPin>(path, transient, resident, cancellationToken).ConfigureAwait(false);
            var record = document.Value;
            this.ValidateRecord(record, path);

            if (record.Kind != kind || !record.References.Contains(reference, StringComparer.Ordinal)) { continue; }

            if (profile.Shared) { return true; }

            var owner = FileSystemAuthority.TryAcquire(profile.AuthorityOptions, this.directory, "stage-reader-" + record.Id);
            if (owner is null) { return true; }

            // A local crash releases the exclusive OS handle. Shared orphan markers never reach this branch.
            try
            {
                File.Delete(path);
                FileSystemDurability.SyncDirectory(this.directory);
            }
            catch { owner.RetainIfShared(); throw; }
            finally { owner.Dispose(); }

            File.Delete(Path.Combine(this.directory, "stage-reader-" + record.Id));
            await release(record.Id, PinCreditBytes, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Reclaims one local orphan reader only after acquiring its existing native owner file under shard authority.</summary>
    /// <param name="shardAuthority">The held authority serializing reader registration and physical retirement.</param>
    /// <param name="id">The generated reader-credit identity selected by bounded root maintenance.</param>
    /// <param name="cancellationToken">Cancels checked pin inspection before physical cleanup.</param>
    /// <returns>True after owned records are deleted and their quota credit returned; false for live, shared or unproven owners.</returns>
    /// <example><code>var reclaimed = await readers.ReclaimLocalOrphanAsync(transaction.Authority, credit.Id, token);</code></example>
    public async ValueTask<bool> ReclaimLocalOrphanAsync(FileSystemAuthority shardAuthority, string id, CancellationToken cancellationToken)
    {
        shardAuthority.EnsureHeld(shardDirectory);
        if (!Guid.TryParseExact(id, "N", out _)) { throw new CacheTransferException(CacheErrorCode.Validation); }

        if (profile.Shared) { return false; }

        var ownerPath = Path.Combine(this.directory, "stage-reader-" + id);
        FileSystemPaths.RejectLinks(ownerPath);
        if (!File.Exists(ownerPath)) { return false; }

        using var owner = FileSystemAuthority.TryAcquire(profile.AuthorityOptions, this.directory, "stage-reader-" + id);
        if (owner is null) { return false; }

        var path = Path.Combine(this.directory, id + ".pin");
        FileSystemPaths.RejectLinks(path);
        if (File.Exists(path))
        {
            using var record = await CacheCheckedMetadata.ReadAsync<CacheReaderPin>(path, transient, resident, cancellationToken).ConfigureAwait(false);
            this.ValidateRecord(record.Value, path);
        }

        foreach (var suffix in new[] { ".pin", ".pin.write.tmp" })
        {
            var candidate = Path.Combine(this.directory, id + suffix);
            FileSystemPaths.RejectLinks(candidate);
            File.Delete(candidate);
        }

        FileSystemDurability.SyncDirectory(this.directory);
        owner.Dispose();
        File.Delete(ownerPath);
        await release(id, PinCreditBytes, CancellationToken.None).ConfigureAwait(false);
        return true;
    }

    private void ValidateRecord(CacheReaderPin record, string path)
    {
        if (record.Format != 1 || record.StoreId != storeId || record.CollectionIdentity != collectionIdentity || record.Shard != shard || record.RecoveryGeneration != recoveryGeneration ||
            !Guid.TryParseExact(record.Id, "N", out _) || Path.GetFileName(path) != record.Id + ".pin" || record.Kind is not ("payload" or "index") ||
            record.References is null || record.References.Count is <= 0 or > CacheManifest.MaximumSegments ||
            record.References.Any(r => !Guid.TryParseExact(r, "N", out _)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    private async ValueTask CloseAsync(FileSystemAuthority owner, string id)
    {
        try
        {
            using var shardAuthority = await FileSystemAuthority.AcquireAsync(profile.AuthorityOptions, shardDirectory, "authority", false, CancellationToken.None).ConfigureAwait(false);
            if (shardAuthority is null) { throw new CacheTransferException(CacheErrorCode.ProviderUnavailable); }

            File.Delete(Path.Combine(this.directory, id + ".pin"));
            FileSystemDurability.SyncDirectory(this.directory);
            owner.Dispose();
            File.Delete(Path.Combine(this.directory, "stage-reader-" + id));
            // Release only after all owned physical records have actually gone; failed cleanup stays charged.
            await release(id, PinCreditBytes, CancellationToken.None).ConfigureAwait(false);
        }
        catch { owner.RetainIfShared(); throw; }
        finally { owner.Dispose(); }
    }

    private sealed class Lease(CacheReaderRegistry registry, FileSystemAuthority owner, string id, ResourceCharge memory) : IAsyncDisposable
    {
        private readonly Lock gate = new();
        private Task closing;

        public ValueTask DisposeAsync()
        {
            lock (this.gate) { return new(this.closing ??= this.CloseAsync()); }
        }

        private async Task CloseAsync()
        {
            try { await registry.CloseAsync(owner, id).ConfigureAwait(false); }
            finally { memory.Dispose(); }
        }
    }

    private sealed record CacheReaderPin(int Format, string StoreId, string CollectionIdentity, int Shard, long RecoveryGeneration, string Id, string Kind, IReadOnlyList<string> References);
}